# Session: hermetic-windows

Moved to Xake.Dotnet on 2026-11-03 from the Xake repository (`docs/features/hermetic-windows/` on
Xake `dev`, plus the round-3/4 interview text of the old branch `feature/hermetic-windows`). The
decisions in `brief.md` and `interview.md` stand; `interview.md` is authoritative over `brief.md`.
Branch for the work that remains here: `feature/hermetic-windows` (from `dev`), one branch for all
open items (owner, 2026-11-03; brief §7).

## State at 2026-11-03

Xake 4.0.0 is released; the Xake.Dotnet 4.0.0 release is in preparation (`release/4.0.0`).
The hermetic library and the .NET tasks live in this repository; the engine stays in
[OlegZee/Xake](https://github.com/OlegZee/Xake).

The Windows findings series (testing the hermetic build of a consumer solution on Windows, locks
recorded on macOS) is **closed**: the first five PRs are merged in the Xake repository and released
(Xake 3.13.0.43, `Xake.Hermetic.Dotnet` 0.8.0.44, tags `v3.13.0` and `hermetic-v0.8.0`).

| PR (Xake repo) | What |
|---|---|
| OlegZee/Xake#93 | PR2-core: msbuild/NuGet diagnostics in `Tool.diagnosticLevel`, error summary |
| OlegZee/Xake#94 | PR1: line-ending-neutral text hashes, targeting packs from NuGet by default (0.8.0) |
| OlegZee/Xake#95 | PR2-hermetic: MSBuild 17.10 floor check, import errors in the log, run in `ProjectRoot` |
| OlegZee/Xake#96 | PR3: secret script variables (core) |
| OlegZee/Xake#97 | bootstrap on Xake 3.13.0.43 / Xake.Hermetic.Dotnet 0.8.0.44 |
| OlegZee/Xake#120 | Windows CI leg: the 7 first-run failures fixed test-side |
| OlegZee/Xake#123 | log flush on exit, `FileLogger` retries a locked previous log |

Closed in the Xake repository (owner decisions, do not reopen): PR4 build log changes (not now;
mostly mitigated by #123) and the engine-side loose end below.

Deferred / open work that belongs here is listed in `tracker.md`. Hermetic code that these items
touch: `src/hermetic/Import.fs`, `Project.fs`, `MsbuildLog.fs`, `Lock.fs`, `Locks.fs`;
`src/dotnet/DotNetFwk.fs` (SDK probe, now local to this repository: `sdkDir` no longer has to be made
public in a core).

## Reading the analyses

`analysis/*.md` and `brief.md` were written against the Xake repository on 2026-10-26. Line numbers
are of that tree and have drifted. `Xake:src/core/...` and `Xake:src/tests/...` refer to the engine
repository; `src/hermetic`, `src/dotnet`, `src/hermetic.tests` and `docs/hermetic*` are here.
`docs/roadmap.md` is Xake's roadmap (linked); the three roadmap sections of the findings are copied
into `tracker.md` as items.

## Next step

1. Wait for the Xake.Dotnet 4.0.0 release (do not touch `build.fsx`, the fsprojs or the locks meanwhile).
2. Then rebase `feature/hermetic-windows` (it exists, with the doc updates only) on `dev` and take the
   tracker's open items in this order, a commit each, one PR:
   a. `Import.RecordOptions.Environment` and `Project.ImportOptions.Environment` (secret environment for the
      import's msbuild runs; decision 15, the core part shipped in 3.13); a minor of the hermetic assembly.
   b. The floor check through the SDK probe and `MSBuild.deps.json` instead of `dotnet msbuild -version`
      (decision 12, second half).
   c. A confirmation run of the 17.10 floor on an SDK 8.0.2xx (decision 13), and a run that checks a missing
      pack root on SDK 8 and 9.
3. Satellites (PR5) stay deferred (S71); take them only if the owner reopens S71 (shape: interview round 4).

## Notes

- The consumer's own validation on Windows (one re-import to record NuGet pack hashes and normalized text
  hashes, decision 29) is the owner's, not an item for code.
- Do not run the consumer's test suites; unit tests of this repository are fine.
- No customer or company names anywhere (public repository): say "the consumer".
- Engine-side loose end, stays in Xake: tests that run scripts without `FileLog` share the bin `build.log`
  and race on Windows (isolate each, or make `FileLogger` tolerate a locked log).
