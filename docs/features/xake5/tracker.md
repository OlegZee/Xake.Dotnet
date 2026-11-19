# Tracker: xake5

One line per item. Status as of 2026-11-19: implemented, not pushed.

## Open

- [ ] Report to the owner; push and PR `feature/xake5` -> `dev` (squash) on the owner's word in this session
- [ ] Windows VM check of the hermetic tests: not needed by the port's changes (tool paths are `getSysFileName`, equal to the old `FullName`); run it if the owner wants it before the preview
- [ ] Release follow-up (owner tags `v5.0.0-preview.1`): both scripts' `#r` to `Xake 5.0.0-preview.2` + `Xake.Dotnet 5.0.0-preview.1`; at 5.0.0 the floor to `[5.0.0, 6.0)`, and the getting-started `#r "nuget: Xake.Dotnet, 4.x"` lines and `[4.0.0, 5.0)` ranges (readme, hermetic-guide, hermetic/README, tasks, lock, workflows) to 5.0.0

## Later (out of scope, proposals for the owner)

- [ ] csc/fsc rules: `also` for the `.pdb` / `.xml` side outputs (Xake 5 checks every output is written)
- [ ] Hermetic rules: header `needs` for the lock and description inputs (built before the body, content cut-off)
- [ ] `requires` instead of `withResource`/`CSC_JOBS` inside the compile recipes (WaitTime accounted by the engine)
- [ ] `bump-base` did not re-resolve Xake in the locks on the 4.0.0-preview.1 bootstrap (its `LOCK_REFRESH` environment variable); check after the scripts move

## Done

- [x] `dotnet fsi build.fsx -- -- build test pack` green: dotnet 172/172, hermetic 368/368; the package depends on `Xake [5.0.0-preview.2, 6.0.0)` (2026-11-19)
- [x] The fixture copy `src/hermetic.tests/fixtures/src/dotnet/xake.project.json` equal to `src/dotnet`'s; the import test expects `[5.` (2026-11-19)
- [x] Locks: Xake 5.0.0-preview.2 in both locks (`update-locks -d LOCK_REFRESH=Xake`), the compiler lines kept at SDK 10.0.403, `xake.fingerprints` recomputed; `build.fsc.fsx check` green (2026-11-19)
- [x] `XakeVersion` `[5.0.0-preview.2, 6.0)`; CI `xake-latest` on `5.*-*` (2026-11-19)
- [x] Tests: `FileDep f`, `TargetDep`, `FileRule ([mask], _, _)`, `Out = Some ..`, the fold annotated, the `Started` log line relative; `getRuleMatch` -> `let! m = matched` (89 lines) (2026-11-19)
- [x] Deprecated aliases: src 74 sites (`getSysFileName`; `.Name` where the code slashed the path), tests 15; 0 warnings in src and tests (FS1253 in RecordTests predates the port) (2026-11-19)
- [x] Errors: `Out`/`Doc` as `File option` (Csc, Fsc, builders, `Described.plan`); Sign's delegated `BuildResult`; Pack's `recipe`; recursive recipes annotated (2026-11-19)
- [x] Docs: release notes "5.0.0 (unreleased)" with the migration table; csc-syntax, tasks, devprocess, hermetic lock/versioning/pack/workflows; doc examples on `matched` (2026-11-19)
- [x] Owner approved the design, bootstrap option B (2026-11-19)
- [x] Trial compile against 5.0.0-preview.2 (scratch copy): the full error list (2026-11-19)
- [x] `build.fsx` runs on `Xake 5.0.0-preview.2` unchanged (one FS0044 at line 135) (2026-11-19)
