# Session: import-file-casing

Branch `feature/import-file-casing` (from `dev` at `68c786b`). Given by the owner (not re-opened):
the import records the repository's own spelling of each file, not the project file's, so that
descriptions and locks recorded on macOS or Windows build on Linux.

## State at 2026-11-04

Research done, no code changed. `brief.md` lists every place the import writes a path taken from
msbuild (description and lock), where the replay reads it and what fails on Linux, the recommended
resolver (git index for tracked files, a directory walk for the rest), the effect on existing locks
(no format bump; one re-import of the miscased projects), the tests, and six decisions for the owner.

Riskiest point: embedded resource names. msbuild derives them from the item's spelling, and
`ResourceManager` lookups are case-sensitive: correcting a resource's `file` must not change its name.

The owner answered all eight decisions (brief §7, 2026-11-04): the git index is the truth (read
with `git ls-files`, never parsed by us); only paths change, never a resource's name or a file's
content; every difference is warned at import; the replay is unchanged (an old miscased lock fails
as stale; the fix is a re-import); `ResXFileRef` files are recorded in the lock; doc `<include>`
references are warned only.

## State at 2026-11-04 (implementation)

Implemented on the branch (see `tracker.md`): `src/hermetic/Casing.fs` (the index from
`git ls-files -z --full-name`, read once per import; `Casing.resolve`, `Casing.Resolver`,
`Casing.argument`, `Casing.line`), wired into `Import.describe`/`writeDescription`/`record`/
`entryOf`/`appOf`/`projectsOf` and the legacy `Project.parseImportWith`/`Project.import`.
The lock field for resx file references is `Compilation.ResxFiles` (a list of
`{ Resx; Ref; Path; Sha256 }`, `Csc.ResxFiles`/`Fsc.ResxFiles` in `src/dotnet`), written only
when non-empty, read as empty when absent; format stays 2. The runner reads each recorded file
for its spelling (`ResxFormat.readWith`). Tests: `src/hermetic.tests/CasingTests.fs` (fixtures
`Casing`, `Resx file references`, `Import casing`).

Choices made inside the binding decisions (for the owner to confirm or change):
- `ResxFiles` records every `ResXFileRef` file under the project root, not only miscased ones
  (the owner's "the pairs"); its hash is not checked at build (a repository file, as a source or
  the resx itself): the build `needFiles` it, so an edit rebuilds; `Lock.diff` shows a change.
- The legacy `Project.import` lock (format 1) does not get `ResxFiles`: decision 6 asked for the
  resolver at `resolveAgainstProject` and the arguments.
- A miscased `ProjectReference` whose assembly is named after the project file: the recorded
  output is named after the repository's project name (`Lib.dll`, not msbuild's `lib.dll`). An
  application's `deps.json` written by the app run still names msbuild's spelling for such a
  reference (the stand-in is named after `ReferencePath`): not handled, open.

## Next step

The owner's review of the branch (no PR opened yet); then the consumer re-imports on macOS or
Windows and builds on Linux (the last tracker item). Open: the `deps.json` spelling of a
miscased project reference of an application (above), if the consumer has one.
