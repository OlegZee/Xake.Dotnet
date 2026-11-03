# Tracker: hermetic-windows

One line per item. Status as of 2026-11-03. "Xake" means the engine repository; PR numbers there are
`OlegZee/Xake#N`.

## Done (in the Xake repository, before the split)

- [x] Analyses (`analysis/lock-content.md`, `engine.md`, `import-msbuild.md`) and `brief.md`; owner interview rounds 1-4 (`interview.md`)
- [x] Customer abbreviation scrubbed from `analysis/lock-content.md`; consumer names scrubbed from the roadmap
- [x] PR1 lock portability (OlegZee/Xake#94): line-ending-neutral text hashes (`FileHash.textSha256`/`textMatches`) for Imports, App.Files, App.config; `RecordOptions.PacksFrom = NuGet | Installed`, NuGet default; a missing pack on a private feed names the cause. hermetic 0.8.0
- [x] PR2 import diagnostics: core classifier in `Tool.diagnosticLevel` (OlegZee/Xake#93), hermetic `MsbuildLog` floor check, msbuild errors in the log, `ProjectRoot` as working directory (OlegZee/Xake#95)
- [x] PR3 secret script variables, core (OlegZee/Xake#96); Xake 3.13.0.43
- [x] Bootstrap bump (OlegZee/Xake#97); releases `v3.13.0`, `hermetic-v0.8.0`; series closed 2026-10-27
- [x] Windows CI leg (OlegZee/Xake#120); log flush on exit and locked previous log (OlegZee/Xake#123)
- [x] Feature folder moved to Xake.Dotnet (2026-11-03)

## Open (in this repository, after the 4.0.0 release)

- [ ] `Import.RecordOptions.Environment` / `Project.ImportOptions.Environment`: explicit secret environment for the import's msbuild runs (today only `NUGET_PACKAGES`; `pexec` adds to the inherited environment). Next
- [ ] Floor check through the SDK probe (`DotNetFwk`) + `MSBuild.deps.json` instead of `dotnet msbuild -version` (the old "needs `sdkDir` public in the core" blocker is gone: both live here)
- [ ] Confirm the 17.10 / SDK 8.0.300 floor on an SDK 8.0.2xx (decision 13)
- [ ] Verify a missing pack root on SDK 8 and 9 imports (the tests ran on SDK 10.0.402 only)

## Deferred (owner decisions, not started)

- [ ] Satellite assemblies in imported locks (PR5, S71 stays closed): capture msbuild's satellite `Csc` call, fail under invariant globalization, C# first (interview 25-28); needs a second re-import at the consumer
- [ ] Build log defects (PR4, owner: not now; flush on exit fixed by OlegZee/Xake#123; remaining: name clash, relative name, hard-coded "See build.log"): engine-side, tracked in Xake
- [ ] Consumer validation on Windows (the owner's)

## Engine-side, stays in Xake

- [ ] Tests that run scripts without `FileLog` share the bin `build.log` and race on Windows (candidates: CommandTests, DescriptionTests, FileTasksCopy, FileTasksRm, FlatScriptTests, FileTasksTests, ParentDirTargetTests, ProjectPlanTests, RuleSpecificityTests, StaleArtifactDepTests): isolate each, or tolerate a locked previous log
