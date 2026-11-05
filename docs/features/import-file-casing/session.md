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

The owner confirmed the choices (brief §7, answers 9-12, 2026-11-05): `ResxFiles` records every
`ResXFileRef` file, its hash is recorded and not checked; the release is 4.1.0. Answer 9 is done:
`Import.depsFileNames` renames a referenced project's runtime file in an application's recorded
`deps.json` (`core.dll` -> `Core.dll`, and the file of a `$(AssemblyVersion:...)` token); the
library and assembly names stay msbuild's (the host finds the file by the asset path, the runtime
binds by the assembly's own name, so they need not match). The fixture `Import casing` has an
exe referencing a miscased library: the recorded deps.json, the run folder from the locks, and
the app runs.

The flake `imports the toolset compiler from the project` (hermetic/rest): 3 full runs of
`origin/dev` in a temporary worktree all passed; on this branch it failed once in 3 full runs
(the lock named `~/.nuget/packages` files untokenized: msbuild restored outside the test's
package folder) and passed alone and in every run since. Not reproduced on dev, not explained;
the casing resolver is a no-op for that test (its folder is in no git index). Not fixed.

## Next step

The owner's review and PR to `dev` (none opened); then the consumer re-imports on macOS or
Windows and builds on Linux (the last tracker item).
