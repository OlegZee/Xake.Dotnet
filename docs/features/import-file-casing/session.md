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

## State at 2026-11-06

- `origin/dev` merged (WPF record, 4.1.0 released): this feature is 4.2.0 (release notes
  "4.2.0 (unreleased)"). Dev's `referencedProject` takes the casing resolver; the WPF markup
  inputs and the markup run's referenced outputs are resolved (names stay msbuild's).
- The resolver is per path: `Casing.Repos` finds the repository of each path's nearest existing
  folder (`git rev-parse --show-cdup`, cached per folder) and reads each repository's index once;
  a submodule is its own repository, its path spelled as its superproject's index has it. Before,
  only the index of the script's project root was read, so the consumer's sources (a sibling
  repository behind a declared root, mostly in submodules) were not corrected at all (the
  consumer's Linux run on 4.2.0-casing.1: 0 changes, 0 warnings). Test folders of this repository
  (`~testout~`, ignored) are in its index as nothing: unchanged.

Flakes, root cause (2026-11-06): **the IDE builds the test fixtures while the tests run.** VS
Code's C# Dev Kit (open on the main checkout, so every worktree under `.claude/worktrees/` is in
its workspace) and its reusable MSBuild nodes (`/nodeReuse:true`; no test starts one) evaluate,
restore and build the projects the tests write under `src/dotnet.tests/~testout~/`. Evidence:
- `lsof` sampled every 0.5 s during full runs: the Dev Kit's nodes (children of its process) have
  their working folder or files in the fixtures that flake (`import-record-chain`, `-redirects`,
  `-apps`, `run-folders`, `toolset-import`) in every run (49 to 136 samples per run); in a
  failing run the node sat in `import-record-chain/b` exactly while `c`, referencing `b`, failed
  for `b`'s missing types;
- symptoms are those of a second build in the folder: a fixture folder "Directory not empty" while
  a test deletes it (also `git worktree remove` of a temporary worktree after its tests ended,
  with a Dev Kit node holding a fixture folder open), a design-time build resolving no project
  reference, the legacy import reading an `obj/project.assets.json` whose package paths are
  `~/.nuget/packages` (the IDE's restore, not the test's package folder);
- the branch-point code (`68c786b`, no casing code) fails the same tests in this worktree
  (1 in 3 interleaved runs), and the branch's code outside the IDE's workspace (a temporary
  worktree under `$TMPDIR`) passed 8 of 8 (and the tip 5 of 5, below);
- the resolver is a no-op for those fixtures (their files are in no index); bisecting it away
  (`git` not run, the content scan skipped) did not change the failures.
The feature adds integration fixtures (more projects for the IDE to load, longer runs), which
likely raises the rate in this worktree. Not a defect of the feature code; not fixed (closing
the folder in the IDE, or test folders outside the workspace, would remove it: a decision for
the owner). Counts: this worktree, branch: 13 failing runs of 38; branch outside the IDE: 0 of
8; tip after the redesign: 4 of 5 green here, 5 of 5 in a row outside the IDE workspace.

Consumer rerun on Linux from 903129b (2026-11-06): 13 paths corrected, no stale lock, no name
changed. Two gaps, fixed in 111db26: `ResxFiles` was filtered to the project root (now: a
repository file under any root of the lock, `recordsResxFile`; the log line says whether it was
recorded), and the include scan now follows csc's order (the source's folder, then the project
folder). Full `build test` 5 of 5 in a row in a temporary worktree under `$TMPDIR` (outside the
IDE's workspace), `build.fsc.fsx clean build check` green.

## Next step

The coordinator's Linux consumer rerun from the pushed tip (the resx images and the includes);
then the PR. Open: whether to keep test folders out of the IDE's workspace (owner).

## Consumer check on Linux (2026-11-06)

The consumer at its last commit before the WPF work, re-imported on macOS with a local package of
`af99f32` (`4.2.0-casing.3`), built in a Linux container on a case-sensitive file system with no case
aliases. Its sources are a sibling repository (a declared root) with git submodules.
- Import: 13 paths corrected (analyzer configs, a `Directory.Build.props` import, one source, 7
  project references), 39 miscased `ResXFileRef`, 812 doc `<include>` warnings. No resource or
  manifest name changed. `ResxFiles`: about 16,000 entries in 60 locks (every `ResXFileRef` file,
  answer 12), the only other lock change.
- Linux: all four cells built (186/186/184/184 compilations) and both brands' app run folders were
  written; every runtime asset of the 88 `deps.json` exists with its exact spelling. The only source
  change was the 812 include spellings, generated from the import's warnings alone.
- Two earlier runs found the gaps fixed since: the resolver read only the script's repository (now
  the containing repository of each path, submodules included), and `ResxFiles` and the include scan
  were limited to the project root / the source's folder.
- The test flakes in this worktree come from the IDE (C# Dev Kit) building the test fixtures under
  `~testout~` while tests run; outside the IDE's workspace the suite is green 5 of 5.
