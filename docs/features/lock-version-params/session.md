# Session: lock-version-params

Branch `feature/lock-version-params` from `dev` at `f5294e0`. Goal and design: `brief.md`
(the brief came from a consumer; "Decisions taken" at its end); items: `tracker.md`.

## State at 2026-11-07

- Commits: `944d942` (code, tests, `lock.md`), `1981406` (`docs/hermetic/versioning.md`), then
  the suffix check and test gaps (the commit after it). Not pushed, no PR. The worktree is `.claude/worktrees/lock-version-params-xake-8fa981`.
- Implemented: `src/hermetic/Import.fs` (`RecordOptions.VersionToken`, `versionMarker`,
  `recordGlobals`, `versionTokenize`, `depsVersionTokens`, `entryOf`, `record`: `VERSION`
  emptied, the `Info` line), `Locks.fs` (`Settings.VersionSuffixes`, `carriesVersion`,
  `versionIn`, `entryOfFramework`, `versionOf`), `Apps.fs` (`informationalVersion`,
  `$(Version:<file>)` in `depsResolved`), `Sbom.fs`.
- Tests: `src/hermetic.tests/RecordTests.fs`, the last two fixtures.
- Second commit of the session: the suffix validated (`Locks.suffixProblem`), the test gaps
  closed (acceptance 9 included), `brief.md` corrected; full hermetic tests green (334).
- Preview package: `out/pkg/Xake.Dotnet.4.2.0-version.1.nupkg` (local only, rebuild with
  `dotnet fsi build.fsx -- -- pack -d VERSION=4.2.0-version.1`).
- `feature/import-file-casing` (#12, not merged, not in 4.2.0) rewrites much of `Import.fs`; the
  conflicts it would meet are in `release.md`. Do not merge it here without the owner's word.

## Next step

Release 4.2.0 with this feature only, public, no preview: `release.md` (push, draft PR to
`dev`, merge, release commit, tag). `feature/import-file-casing` (#12) is not merged and not
part of 4.2.0. The PR text is prepared; push and PR only on the owner's OK.
