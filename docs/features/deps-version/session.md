# Session: deps-version

Branch `feature/deps-version`, cut from `dev` at `b4853da` (4.3.0). Task B-3 of the consumer
analysis: the project version in an application's `deps.json` from the lock (`brief.md`).

## Where things stand (2026-11-19)

- `design.md` written and reported to the orchestrator session; no code yet.
- Scope: `src/hermetic/Apps.fs`, `src/hermetic/Locks.fs`. `Import.fs` is not touched (open PRs
  #16, #12 and tasks B-1/B-4/B-5 change it); point 4 of the brief needs no import change.
- Local test baseline: this Mac has SDK 8.0.427 and 11.0.100 only, so 7 `hermetic/rest` tests
  fail on clean dev; `build.fsc.fsx build check` (SDK 10.0.403) runs only in CI (`build-fsc`).

## Next step

Wait for the owner's word on `design.md`. Then: docs, code, tests as in `tracker.md`, run
`dotnet fsi build.fsx -- -- build test pack`, report; no push or PR without the owner.
