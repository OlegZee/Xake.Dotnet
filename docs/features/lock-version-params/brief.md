# Brief: lock-version-params

Record a project's version in its lock as a token. The build fills it in from a per-build value, so a
consumer can stamp a build's version (`1.4.0-alpha-512`) into assemblies, `deps.json` and packages
without recording the lock again.

## Today

A recorded lock (`Import.record`) holds the version msbuild computed at import, as literals:

- `Compilation.Generated`, the SDK's `<project>.AssemblyInfo.cs` (`.fs`):
  ```csharp
  [assembly: System.Reflection.AssemblyFileVersionAttribute("1.4.0.0")]
  [assembly: System.Reflection.AssemblyInformationalVersionAttribute("1.4.0")]
  [assembly: System.Reflection.AssemblyVersionAttribute("1.4.0.0")]
  ```
- `Evaluation.Properties.Version` (`"1.4.0"`), which a consumer reads for its package version;
- an application's recorded `deps.json` (`<lock>.files/<tfm>/app/<app>.deps.json`): the project
  libraries as `"lib/1.4.0"`, their dependencies as `"lib": "1.4.0"`, SDK 8's
  `"assemblyVersion": "1.4.0"` for a referenced project. The file versions are already tokens
  (`$(AssemblyVersion:<file>)`, `Import.depsTokens`).

Nothing else depends on the version: no compiler switch, and csc builds the Win32 version resource
from the attributes. The BAML and `Recorded.Markup` name `AssemblyVersion` only.

A consumer whose CI stamps a per-build suffix (the usual scheme: `Version = <prefix>-<suffix>`,
`AssemblyVersion = FileVersion = <prefix>.0`, the build number only in the suffix) can either
re-import on every build (minutes of msbuild per build, which defeats the lock) or ship assemblies
whose `InformationalVersion` lacks the suffix while the package has it.

In a typical multi-repository solution several groups of projects carry different prefixes
(`app/**` at 3.1.0, `vendor/core/**` at 1.4.0, each from its own `Directory.Build.props`) and may
take different suffixes (a distribution variant adds `-eu` to one group only).

## What is already in place

- **`$(Version)` in `Generated`**: `Locks.compile` replaces the token `Locks.VersionToken` with the
  variable of the first `Locks.Settings.Versions` mask matching the project folder (`versionFor`,
  roots allowed: `$(Sibling)/**`). The variable is read, and so is a dependency, only when the token
  is there; a token with no matching mask fails the build. Tested in `BuildFromLockTests.fs`
  ("`$(Version)` substituted and a new version rebuilds"). No lock carries the token today: neither
  `Locks.generate` nor `Import.record` writes it (`docs/hermetic/lock.md`, "assembly versioning is
  an open topic").
- **A value tokenized at record and resolved at build**: `$(SourceRevisionId)`. The import replaces
  the commit sha with the token in every text of the entry (`Lock.mapText (Git.tokenize sha)`:
  `Generated`, `Options`, `Defines`, `Evaluation.Properties`), and `Lock.compileFrom` resolves it
  back before the compile.
- **Tokens in recorded application files**: `$(AssemblyVersion:<file>)` and `$(FileVersion:<file>)`
  in `deps.json`, resolved by `Apps.depsResolved` when the application is written.
- **The prefix is an import input**: `VersionPrefix` lives in an msbuild file that is in
  `Evaluation.Imports`, so a new prefix already fails `Locks.check` and the build ("run the import
  again"). That is correct: a new prefix changes `AssemblyVersion`, which the BAML pins.

## Proposal

1. **Record with a marker suffix.** A new option, `RecordOptions.VersionSuffix: string option`
   (`None` by default: today's behaviour). With `Some marker` the import:
   - adds the global property `VersionSuffix=<marker>` to every msbuild run (the marker is a valid
     SemVer pre-release label that occurs nowhere else, e.g. `xakeversionmarker`). The SDK then
     derives `Version = <VersionPrefix>-<marker>` and carries it wherever `$(Version)` goes:
     `InformationalVersion`, `PackageVersion`, `deps.json`, any custom generated file.
     `AssemblyVersion` and `FileVersion` drop the pre-release part (`<prefix>.0`), so the BAML,
     `Recorded.Markup` and binding redirects are unaffected;
   - records `VersionPrefix` in `Evaluation.Properties` (add it to `designTimeProperties` and
     `keptProperties`);
   - replaces `<VersionPrefix>-<marker>` with `$(Version)` in every text of the entry
     (`Lock.mapText`) and in the recorded application files (`deps.json`, configs);
   - fails, naming the file, when the marker is left anywhere else (a target that uses
     `$(VersionSuffix)` on its own, or a version built some other way), and when `AssemblyVersion`
     or `FileVersion` carries it;
   - when a project sets `Version` itself (the marker never reaches it), records it as today and
     says so at `Info` level: that project's version is fixed.
2. **Resolve at build.** `Locks.Settings` gets
   `VersionSuffixes: (string * Recipe<ExecContext, string>) list`, a mask over the project folder
   to the suffix, matched like `Versions` (`versionFor`). It is a recipe, not a variable, so the
   script can compose it (`recipe { let! s = vars.Suffix in return s + "-eu" }`); the variables it
   reads become dependencies as usual. For an entry with the token:
   - a matching `Versions` mask gives the whole version (unchanged behaviour, wins);
   - else a matching `VersionSuffixes` mask gives `VersionPrefix` + `-<suffix>`, or `VersionPrefix`
     alone when the suffix is empty;
   - else the build fails: `'<name>': the lock needs a version ($(Version)) and no mask of
     Versions or VersionSuffixes matches '<project path>'`. There is no silent default, so a
     misconfigured CI cannot ship a release-looking `1.4.0`.

   The value replaces the token in `Generated` (as today), `Options`, `Defines` and
   `Evaluation.Properties` (`Lock.mapText`, as `$(SourceRevisionId)`), and in the application files
   `Apps.write` produces.
3. **The package version.** `Locks.versionOf (settings: Settings) (lockPath: string) (fwk: string)
   : Recipe<ExecContext, string>` returns the resolved `Version` of an entry by the same rules, so a
   script packs with exactly the version its assemblies carry. `Pack.tokensOf` stays as it is (the
   caller passes the version).
4. **`Locks.check` and staleness**: unchanged. The token is not hashed. `VersionPrefix` stays
   covered by `Evaluation.Imports`.
5. **Docs**: `docs/hermetic/lock.md` (the token, the option, the resolution order), the README
   section on versioning, `release-notes.md`.

## Lock format changes

- New token value `$(Version)` in `Generated` content, in `Evaluation.Properties.Version` and in
  recorded application text files. Example of a recorded entry:
  ```json
  "Evaluation": { "Properties": { "Version": "$(Version)", "VersionPrefix": "1.4.0", ... } },
  "Compilation": { "Generated": { "$(ProjectRoot)/obj/xake/core/netstandard2.0/Release/Core.AssemblyInfo.cs":
      "... AssemblyFileVersionAttribute(\"1.4.0.0\")] ... AssemblyInformationalVersionAttribute(\"$(Version)\")] ... AssemblyVersionAttribute(\"1.4.0.0\")] ..." } }
  ```
  and in `deps.json`: `"core/$(Version)"`, `"core": "$(Version)"`.
- New kept property `VersionPrefix`.
- No `FormatVersion` bump. An older Xake.Dotnet reading such a lock already fails on the token in
  `Generated` (no matching `Versions` mask), before it would copy a token into `deps.json`, because
  every tokenized entry has it in its `AssemblyInfo`. The lock's `Version` property holding a token
  is a visible change for scripts that read it; they move to `Locks.versionOf`.

## Compatibility

- Without `RecordOptions.VersionSuffix` nothing changes: same msbuild runs, same locks.
- With it, a re-import changes each entry once (the diff shows the token, `VersionPrefix` and the
  `deps.json` tokens). Then imports with any suffix write the same lock.
- `Locks.Settings.Default` gets `VersionSuffixes = []`. Scripts that build composed or generated
  locks see no difference.
- Incremental builds: a suffix change rebuilds every entry with the token and what references it.
  That cannot be avoided, since the bytes of each assembly change. A constant suffix rebuilds nothing.
  `Versions` behaves as before.

## Acceptance criteria

1. A fixture with two project groups (prefixes 3.1.0 and 1.4.0, one referencing the other, one an
   application on `net8.0`) recorded with `VersionSuffix = Some marker`: the lock has no marker,
   has `$(Version)` in `InformationalVersion`, `Version` and `deps.json`, and has `VersionPrefix`.
   `AssemblyVersion`/`FileVersion` are the literals `x.y.z.0`.
2. Recording again gives a byte-identical lock.
3. Built with `VersionSuffixes = [ "app/**", s1; "**", s2 ]`: the assemblies' `InformationalVersion`
   is `3.1.0-<s1>` and `1.4.0-<s2>`, `AssemblyVersion` and `FileVersion` are `x.y.z.0`; the written
   `deps.json` names `app/3.1.0-<s1>` and `core/1.4.0-<s2>`; `Locks.versionOf` returns the same.
4. An empty suffix gives `3.1.0` exactly (no trailing `-`).
5. A second build with the same suffix compiles nothing. A new suffix recompiles the tokenized
   entries.
6. A tokenized lock built with no matching mask fails with the message above. No literal
   `$(Version)` reaches an assembly or a written file.
7. A project that sets `<Version>` explicitly records as today with an `Info` line. A target that
   writes `$(VersionSuffix)` alone into a generated text fails the import, naming the file.
8. `HERMETIC=on` and `Locks.check` behave as before on a tokenized lock. Editing `VersionPrefix`
   in `Directory.Build.props` makes the lock stale.
9. A WPF fixture recorded with the option: the recorded BAML and `Recorded.Markup` hold
   `AssemblyVersion` only and are identical with and without the option.

## Test ideas

- Pure tests: the tokenizer (`<prefix>-<marker>` → `$(Version)`, a stray marker detected, a prefix
  that is a substring of another version (`1.4.0` in `11.4.0-...`) is left alone), the resolution
  order (`Versions` over `VersionSuffixes`, empty suffix), `versionFor` with root-prefixed masks.
- `RecordTests.fs`: the two-group fixture of criteria 1, 2 and 7. Note the existing test about the
  environment variable `VERSION` (msbuild takes it as `$(Version)`): the marker run must not be
  disturbed by it, so pin it the same way.
- `BuildFromLockTests.fs`: criteria 3–6 on the recorded fixture; read the attributes back with
  `System.Reflection.Metadata`; check that two clean builds with the same suffix are byte-identical.
- Apps: a `net8.0` application's `deps.json` after `Apps.write` with a suffix.

## Open decisions

- **Marker vs. attribute rewriting.** The alternative is to leave msbuild alone and rewrite the
  version attributes in the recorded `AssemblyInfo` by name. It needs a parser per language and misses
  `deps.json`, the `Version` property and custom generated files without telling anyone. The marker
  goes through msbuild's own data flow and fails loudly on what it cannot place. Recommended: marker.
- **The token's name.** Reusing `$(Version)` keeps one substitution path and one meaning ("this
  entry's version"); `VersionSuffixes` only adds a second way to compute it. A separate
  `$(VersionSuffix)` token would leave a dangling `-` for an empty suffix.
- **Where the suffix setting lives.** In `Locks.Settings` (per build, per mask), not in the lock: the
  lock must not depend on the build number.
- **Tokenizing `AssemblyVersion` as well**, so that a prefix change needs no re-import: out of scope.
  It would invalidate recorded BAML identities (`Recorded.Markup`), and a prefix changes with a
  source commit, which re-imports anyway.

## Evidence (a consumer's recorded locks, SDK 8.0.4xx)

- 190 entries per configuration in nine project groups with prefixes from 1.2.6 to 20.2.2. Each has
  exactly the three version attributes above in `Generated`, the literal `Version` property, and no
  `AssemblyMetadata` or version-bearing switch.
- The version is set as `VersionPrefix` in each group's `Directory.Build.props` (or a variant props
  file); the existing CI passes `/p:VersionSuffix=<suffix>` (or `/p:Version=<prefix>-<suffix>
  /p:FileVersion=<prefix>.0`), so the SDK defaults derive the rest. The marker reproduces that data flow.
- An application's `deps.json` carries project versions as literals next to the existing
  `$(AssemblyVersion:<file>)` tokens.
- A WPF entry's BAML names its own assembly as `<Name>, Version=<prefix>.0`, so the suffix does not
  reach it.
- Not probed: the record with the marker itself (msbuild's restore with a pre-release project
  version, the SDK 8 `assemblyVersion` field in `deps.json` under a suffix).

## Decisions taken (2026-11-07, implementation)

- **Marker**, as recommended; `Import.versionMarker` = `xakeversionmarker`. The marker must be
  letters and digits starting with a letter; a script that also sets `VersionSuffix` fails.
- **`deps.json` gets `$(Version:<file>)`, not `$(Version)`.** A probe on SDK 8.0.427 showed
  that an application's `deps.json` names every project with its own prefix
  (`app/3.1.0-<marker>`, `lib/1.4.0-<marker>`), and SDK 8 writes a referenced project's version
  as its `"assemblyVersion"` too. One `$(Version)` cannot stand for both, and the referenced
  project's suffix group is not the application's. The token names the library's runtime file;
  `Apps.depsResolved` reads its informational version (`ProductVersion`, build metadata
  dropped), as it reads `$(AssemblyVersion:<file>)`. The result matches what msbuild writes
  with a real suffix.
- **The prefix is derived from the recorded `Version`** (`<prefix>-<marker>`), so a project
  whose `VersionPrefix` is computed elsewhere still records correctly. It is kept as
  `Properties.VersionPrefix`.
- **`VERSION` in the environment is emptied** for a marker import: msbuild would take it as
  `$(Version)` and the marker would never appear (a release workflow exports one).
- **Substitution at build covers `Options` and `Defines` too** (`Csc.mapText`/`Fsc.mapText`),
  not only `Generated`; `Versions` keeps its meaning and wins over `VersionSuffixes`.
- **The SBOM's root version**: when the recorded `Version` is a token, read from the built
  assembly.
