# Session: wpf-record

Branch `feature/wpf-record` from `dev` at `v4.0.0` (2026-11-05). Goal and design: `brief.md`;
items: `tracker.md`.

## State at 2026-11-05

- Nothing in `src/` changed yet. The refusal is at `src/hermetic/Import.fs` ("WPF is not supported
  by the import yet"), its test at `src/hermetic.tests/RecordTests.fs`.
- The application run in `Import.record` (search `appRuns`, `stubAssembly`) is the model for the
  markup run; `entryOf` already records `*.g.cs` as `Generated` and `*.g.resources` beside the
  lock.
- A consumer script on Xake 4.0.0 / Xake.Dotnet 4.0.0 is ready to try the branch: it excludes its
  WPF projects today and builds byte-identically to 3.14.

## Next step

Write the fixture (tracker, first item) and a failing record test against it, then the markup run.
