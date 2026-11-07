# Session: lock-version-params

Branch `feature/lock-version-params` from `dev` at `f5294e0`. Goal and design: `brief.md`
(the brief came from a consumer; "Decisions taken" at its end); items: `tracker.md`.

## State at 2026-11-07

- Commits: `944d942` (code, tests, `lock.md`), `1981406` (`docs/hermetic/versioning.md`), then
  the suffix check and test gaps (the commit after it). Not pushed, no PR. The worktree is `.claude/worktrees/lock-version-params-xake-8fa981`.
- Implemented: `src/hermetic/Import.fs` (`RecordOptions.VersionSuffix`, `versionMarker`,
  `recordGlobals`, `versionTokenize`, `depsVersionTokens`, `entryOf`, `record`: `VERSION`
  emptied, the `Info` line), `Locks.fs` (`Settings.VersionSuffixes`, `carriesVersion`,
  `versionIn`, `entryOfFramework`, `versionOf`), `Apps.fs` (`informationalVersion`,
  `$(Version:<file>)` in `depsResolved`), `Sbom.fs`.
- Tests: `src/hermetic.tests/RecordTests.fs`, the last two fixtures.
- Second commit of the session: the suffix validated (`Locks.suffixProblem`), the test gaps
  closed (acceptance 9 included), `brief.md` corrected; full hermetic tests green (334).
- Preview package: `out/pkg/Xake.Dotnet.4.2.0-version.1.nupkg` (local only, rebuild with
  `dotnet fsi build.fsx -- -- pack -d VERSION=4.2.0-version.1`).
- `feature/import-file-casing` (not merged) rewrites much of `Import.fs`: expect conflicts in
  `entryOf` and `record` when both land. Do not merge it here without the owner's word.

## Next step

The owner runs `consumer-check.md` on the consumer solution; act on what it reports. Then, on
the owner's OK only: push `origin feature/lock-version-params` and a draft PR to `dev` (the
feature, a link to `docs/hermetic/versioning.md`, the open items).
