# Tracker: sdk11

One line per item. Status as of 2026-11-19: fixed locally (build test pack green on SDK 11.0.100, macOS), not pushed.

## Open

- [ ] Owner: push `bugfix/sdk11`, PR -> `dev`; CI on the SDK 11 runner images (`build`, `xake-latest`, `windows`)
- [ ] Windows: not run locally; the variables are set (not removed), so the runtime's handling of null values does not matter there

## Done

- [x] Child `dotnet restore`/`dotnet msbuild` get the MSBuild variables of their own SDK when the parent has the CLI's (`DotNetFwk.childMsbuildEnvironment`): `downloadPackages`, `restoreSync`, `Described.runRestore`, `Project.runMsbuild`, `requireImportMsbuild`, the `msbuild` task of an SDK (in the task, not in `EnvVars`: a non-empty `EnvVars` turns the csc server off) (2026-11-19)
- [x] Tests: `RecordTests`' two `runDotnet` helpers drop the variables like `SatelliteTests`' (an SDK 10 fixture built with SDK 11's MSBuild hung on a reused node; CI's "test host crashed") (2026-11-19)
- [x] `ArgsCore.unquotePath`: a one-path switch value `"<path>"` (SDK 11's `/sourcelink:`) loses its quotes; unit test (2026-11-19)
