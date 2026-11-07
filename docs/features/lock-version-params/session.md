# Session: lock-version-params

Branch `feature/lock-version-params` from `dev` at `f5294e0`. Goal and design: `brief.md`
(the brief came from a consumer; "Decisions taken" at its end); items: `tracker.md`.

## State at 2026-11-07

- Implemented: `src/hermetic/Import.fs` (`RecordOptions.VersionSuffix`, `versionMarker`,
  `recordGlobals`, `versionTokenize`, `depsVersionTokens`, `entryOf`, `record`: `VERSION`
  emptied, the `Info` line), `Locks.fs` (`Settings.VersionSuffixes`, `carriesVersion`,
  `versionIn`, `entryOfFramework`, `versionOf`), `Apps.fs` (`informationalVersion`,
  `$(Version:<file>)` in `depsResolved`), `Sbom.fs`.
- Tests: `src/hermetic.tests/RecordTests.fs`, the last two fixtures.
- `feature/import-file-casing` (not merged) rewrites much of `Import.fs`: expect conflicts in
  `entryOf` and `record` when both land.

## Next step

Pack a local preview (`dotnet fsi build.fsx -- -- pack -d VERSION=4.2.0-version.1`) and record
the consumer solution with the option; then the PR.
