# Session: legacy-recipe

Branch `feature/legacy-recipe` from `dev` at `b82100b` (B-5 #20 included). Task B-4: legacy
(non-SDK) csproj are not supported natively; an import overlay recipe in the docs
(`docs/legacy-projects.md`) and a guard that fails such an import clearly. Goal: `brief.md`.

## State at 2026-11-19 (implementation)

Owner approved the design (decisions in `brief.md`). Implemented and committed locally, not pushed.

- `docs/legacy-projects.md` (122 lines): the four sections, the overlay as one xml block (the tests
  and the Windows kit read it from there), how to pass it (an absolute path: msbuild resolves a
  relative one against its own targets folder), the guard's message, limits.
- `Import.fs` +24/-2: `legacyGaps` (A, B, D) and five probe properties; the guard right after the
  outer evaluation in `evaluate` (`record` and `project`).
- `LegacyProjectTests.fs` (109): the docs' overlay records a legacy library with a package and XAML
  and the lock builds; an SDK project's lock differs only by the overlay as an input; no overlay ->
  A, B, D; section A alone -> B, D; `legacyGaps` over dumps.
- Links (`hermetic/index.md`, `hermetic/lock.md`), release notes "Unreleased".
- `dotnet fsi build.fsx -- -- build test pack`: green, 537 tests in 8 slices (was 532).
- Windows acceptance (`windows/`, package `4.4.0-legacy.1`): SDK 10.0.404 and 8.0.428 pass.

## Next step

The owner's review. On their word given in this session: push `feature/legacy-recipe`, PR to
`dev` (squash). Then Xake.Dotnet 4.4.0 is tagged.

## Probe (2026-11-19, before the design)

The design report (probe results, the recipe as tested, the guard,
the fixture, the line budget) is in the orchestrator's notes folder:
`legacy-recipe-design-2026-11-19.md`. Key findings:

- Today: the restore passes, then `evaluate` stops with `msbuild reports no TargetFramework(s)`
  (`Import.fs`, `evaluate`, shared by `record` and `project`). With partial overlays: MSB3644 (no D),
  or `the compiler ... cannot be named portably` (no B: a legacy project has no `NETCoreSdkVersion`).
- Section D is still needed after B-5 (the implicit reference comes from an SDK target a legacy
  project does not import); it becomes one item reading B-5's globals.
- Section B: condition `'$(MSBuildRuntimeType)' == 'Core'` (`$(NuGetTargets)` is set after the
  overlay is imported); needs `Microsoft.NETCoreSdk.BundledVersions.props` besides the consumer's
  imports; four stub targets and the apphost name all required (ablated on SDK 8 and 11).
- Section C works on macOS too (`UseWPF` + `Microsoft.WinFX.targets`); Windows VM: BAML and `pkg:`
  reference assemblies on SDK 8.0.428 and 10.0.404 (plain msbuild with the import's properties).
- SDK projects: the overlay changes nothing but its own entry as an import input.

Probe material (session scratchpad, not kept): the probe test was a temporary
`LegacyProjectTests.fs` over `RecordKit` with `PROBE_DIR`/`PROBE_OVERLAY`; removed from the tree.
