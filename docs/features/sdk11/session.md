# Session: sdk11

**Released in 4.4.0 (2026-11-19).** Only the "Later" items of `tracker.md` are open.

Branch `bugfix/sdk11` from `dev` (`b4853da`). GitHub's runner images ship SDK 11.0.100 since
2026-11-16; `global.json` (8.0.0, `rollForward: latestMajor`) runs the tests on it, and CI
failed on every PR (first seen on PR #16).

## Findings

- **Inherited MSBuild variables.** `dotnet test` sets `MSBuildSDKsPath`,
  `MSBuildExtensionsPath`, `MSBUILD_EXE_PATH` for its own SDK; the test host and every child
  inherit them, and the child's CLI keeps them. A child whose `global.json` picks SDK 10 then
  loads SDK 11's targets (MSB4216, MSB4027). Removing them is not possible through Xake's
  `shell`/`pexec` (it only sets values): a null value drops a variable on a .NET 10+ runtime but
  is passed as empty on .NET 8 (the test host), and an empty `MSBuildSDKsPath` fails too
  (MSB4236). So the fix sets them to the child's SDK (`sdkProbe`: `dotnet --version` in the
  project root), only when the parent has any of them.
- **Quoted `/sourcelink:`.** SDK 11's tasks report `/sourcelink:"<path>"` (also `/features:"..."`
  and `/embed:"..."`, which are not one-path values and were fine). `ArgsCore` kept the quotes
  for a `Path`-shaped switch; `Project.fs`'s lock import absolutized it into `<dir>/"<path>"`.
  (`Import.fs` already unquoted its arguments, `Import.unquote`.)
- **Test helper hang.** `RecordTests.runDotnet`/`RecordKit.runDotnet` ran `dotnet build` in an
  SDK 10 fixture with the host's SDK 11 variables: SDK 11's MSBuild started a reused node
  (`nodeReuse:true` despite `MSBUILDDISABLENODEREUSE`) that held the redirected output, and
  the test waited until `--blame-hang` killed it (CI's "Test host process crashed" in
  `hermetic/rest`). The helpers now drop the variables, as `SatelliteTests.runDotnet` did.
- **Compiler server.** The csc runner skips the server when the toolchain's `EnvVars` is not
  empty (`Csc.serverArgs`), so the `msbuild` task's variables live in the task, not in `EnvVars`.

## Next step

Report to the owner; on approval push and open the PR to `dev`. No CI SDK pin was needed.
