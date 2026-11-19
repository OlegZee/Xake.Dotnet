# Brief: lock-inputs

Record the inputs of prebuilt binary files a lock keeps (`<lock>.files/<tfm>/`), so editing one
makes the lock stale. Owner's variant (2026-11-19): all declared inputs in one common list.
Status: approved by the owner (2026-11-19), all three decisions as proposed; implementing.

## The problem

The import copies every non-text file msbuild generated and handed to the compiler into
`<lock>.files/<tfm>/` (`Import.fs`, `entryOf`, `binaries`): a WPF `g.resources`, a `.licenses`,
resources a menu compiler made from its source and images. The build takes them as they are.
Their inputs are recorded only for WPF (`markupSources`: `Page`/`ApplicationDefinition`/`Resource`
into `Evaluation.Imports`). Any other tool's inputs are not: editing a menu source, its PNG or
its `.h` does not make the lock stale, and the build silently takes the old resource. The import
cannot know what an `obj` file was made from; the project (an add-on's msbuild file) has to say.

## What exists

- No property named `XakeImport` exists. What a project's msbuild files can see during an import
  today: the global property `XakeRecordKey` (every msbuild run of `Import.record`: restore,
  evaluations, design-time builds, the markup run; `Import.fs` `extensions`), documented only as
  an internal detail (`lock.md`, step 3). A project's own `CustomAfterMicrosoftCommonTargets` is
  chained (`XakeChainedAfterCommonTargets`), so its targets still run.
- The design-time build's items reach the import through the dump target `XakeRecordDump`
  (`dumpTargets`), which writes every item type of `designTimeItems`. Items a target adds before
  `Compile` are in it.
- `Evaluation.Imports` is the one hashed list a build and `check` read: `Locks.staleOf` (both
  `staleImports` at build and `staleWith`/`check`), message "... changed since the import that
  wrote it; run the import again", `LOCK_STALE=warn` honoured.
- `FileHash.textMatches path sha` already accepts the raw `sha256` as well as the line-end
  neutral `textSha256` (for older locks).

## Design

1. **Detection, the "XakeImport" hook.** `Import.record` adds the global property
   `XakeImport=true` to every msbuild run (beside `XakeRecordKey`; one line in `extensions`).
   It is the documented, stable name a consumer tests (`'$(XakeImport)' == 'true'`);
   `XakeRecordKey` stays an internal detail. A global property flows to referenced projects as
   `XakeRecordKey` does.
2. **Declaration.** An item type `XakeImportInput`: the files a generated binary is made from,
   relative to the project or absolute, wildcards as msbuild expands them. Declared statically
   (an `ItemGroup`) or in the add-on's target that makes the binary (it runs before `Compile`,
   so before the dump). One line in `designTimeItems` (no metadata). Consumer example:

   ```xml
   <ItemGroup Condition="'$(XakeImport)' == 'true'">
     <XakeImportInput Include="Menus/menu.src;Menus/*.png;Menus/ids.h" />
   </ItemGroup>
   ```

   The condition is optional (outside an import nothing reads the item); it is the hook for an
   add-on that wants to do more (or less) during an import.
3. **The lock field: none new.** Declared inputs join `Evaluation.Imports`
   (`ctx.Inputs @ appSources @ markupSources @ declaredInputs`, distinct), as `markupSources`
   does. No format change, no new key; an old lock reads and checks unchanged.
4. **Which files.** Each item made absolute against the project folder. Dropped: a file under the
   package root, `NuGetPackageRoot` or an SDK folder (pinned by version and Sha512; the same test
   as `repositoryFiles`' `isSdkFile`), and one under the import's intermediate folder (generated).
   A declared file that does not exist fails the import naming the project and the item (a
   declaration is explicit; silently dropping it would hide the very staleness this is for).
5. **Hashing by kind.** `FileHash.inputSha256 path`: the raw `sha256` for a binary file, the
   line-end neutral `textSha256` for text. Binary = a NUL byte in the first 8000 bytes (git's own
   test; a PNG, an ico, a `.res` have one; a menu source, a `.h`, XAML have none; a UTF-16 text
   file counts as binary, which only means its line ends are not normalized). Not by extension:
   no list to maintain. `Locks.staleOf` needs no change: `textMatches` already accepts a raw hash.
   `Lock.rehash` uses `inputSha256` too, so a rehash agrees with the import.
6. **WPF.** `markupSources` stays (WPF's inputs are known without a declaration). Proposed (small,
   owner to agree): the one `inputSha256` applies to the whole `Imports` list, so WPF `Resource`
   images get the raw hash too. Effect: a re-import of a WPF project with image resources changes
   those lines' hashes once (a lock diff, not staleness: the old text hash still matches through
   `textMatches`). Without it, binary WPF resources keep the text hash (a CRLF pair inside an
   image is invisible to the check). Moving WPF onto `XakeImportInput` is not proposed: it would
   only move the same list behind a declaration.
7. **The check path.** Unchanged code: `staleImports` (build, `needFiles` on the list, then
   `staleOf`) and `staleWith`/`check`. A changed or missing declared input gives
   "'<name>' (<tfm>): the lock '<lock>' is stale: '<file>' changed since the import that wrote it;
   run the import again".

## Out of scope

- Post-compile outputs (a file a tool makes from the built dll): the consumer's build step, not
  the import. See `tracker.md`, "Later".
- Per-binary input lists (which input made which file): the simple variant is one list.

## Fixture and tests (`src/hermetic.tests/RecordTests.fs`)

A C# library whose own targets file (an add-on stand-in, generic names) has a target before
`CoreCompile` that concatenates `menu/menu.src` and `menu/icon.png` into
`$(IntermediateOutputPath)Menu.bin` and adds it as an `EmbeddedResource` (`LogicalName`), so the
compiler gets `/resource:obj/.../Menu.bin` and the import copies it to `<lock>.files/`. The same
file declares `XakeImportInput` for both sources under `'$(XakeImport)' == 'true'`.

- record: `Imports` holds `menu.src` with its text hash and `icon.png` with its raw hash; the
  binary is in `<lock>.files/`.
- one byte of `icon.png` changed: `Locks.staleWith` names it; the build fails "run the import
  again"; `LOCK_STALE=warn` warns. Restored: current again. A CRLF-only edit of `menu.src`: current.
- a declared file under the package root: not recorded; a declared missing file: the import fails.
- without the declaration (the project as before): the entry is byte-identical to today's (old
  locks unchanged). The WPF tests stay green.
- `FileHash.inputSha256`: unit tests for text, binary, UTF-8 BOM, empty file.

## Line budget

| File | Change | Lines |
|---|---|---|
| `src/hermetic/Lock.fs` | `FileHash.inputSha256` (+ `isBinary`), `rehash` uses it | ~15 |
| `src/hermetic/Import.fs` | `XakeImport=true`; `XakeImportInput` in `designTimeItems`; `declaredInputs` (filter, fail on missing); `Imports` hashed by `inputSha256` | ~25 (4 places, `entryOf` and `record` only: stays clear of PR #12's casing changes) |
| `src/hermetic/Locks.fs` | doc comment of `staleOf` | ~3 |
| `src/hermetic.tests/RecordTests.fs` (+ `LineEndTests.fs`) | fixture and tests above | ~110 |
| `docs/hermetic/lock.md` | "Declared inputs" section; `Imports` row; hashing by kind | ~45 |
| `docs/hermetic/workflows.md` | how-to: "Generated resources and their inputs" (consumer side) | ~35 |
| `docs/release-notes.md` | "Unreleased" | ~8 |

Code ~45 lines, tests ~110, docs ~90. No lock format change, no package change (so no
`update-locks` expected; the fsc self-build `check` is run anyway).

## Decisions (approved by the owner, 2026-11-19)

1. The property name `XakeImport` (value `true`) and the item name `XakeImportInput`.
2. A declared file that is missing fails the import.
3. The whole `Imports` list is hashed by kind, WPF images included.
