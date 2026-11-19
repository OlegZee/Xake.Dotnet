# Session: deps-version

Branch `feature/deps-version`, cut from `dev` at `b4853da` (4.3.0). Task B-3 of the consumer
analysis: the project version in an application's `deps.json` from the lock (`brief.md`).

## Where things stand (2026-11-19)

- Design approved by the owner (`brief.md`, Decision); implemented as `design.md` says, plus its
  "Found while implementing" notes. Committed on `feature/deps-version`, not pushed.
- Sizes: `Locks.fs` +37/-2, `Apps.fs` +17/-10; tests +65; docs +40.
- `build test pack` locally: dotnet/rest 2 and hermetic/rest 4 failures (satellites, resource
  sets, an fsproj+csproj lock, msbuild bytes) and a test-host crash in the satellite key test --
  all the same on clean dev `b4853da` (SDK 8.0.427 + 11.0.100 here, no 10.0.x). Every other slice
  passes, the version-token fixture included. `Import.fs` untouched.

## Next step

The owner's word on the report: then push `feature/deps-version`, open the PR into `dev`
(squash), watch CI (`build-fsc` checks SDK 10.0.403; SDK 11 failures are another session's).
