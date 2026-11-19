# Session: legacy-recipe

Branch `feature/legacy-recipe` from `dev` at `b82100b` (B-5 #20 included). Task B-4: legacy
(non-SDK) csproj are not supported natively; an import overlay recipe in the docs
(`docs/legacy-projects.md`) and a guard that fails such an import clearly. Goal: `brief.md`.

## State at 2026-11-19

Probe and design done; no code. The design report (probe results, the recipe as tested, the guard,
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

## Next step

Wait for the owner's approval of the design (open: the guard's "(missing: …)" suffix and B/D
checks beyond the brief's TF-only check; C in the same file vs a separate one). Then: docs first
(`docs/legacy-projects.md`), the guard in `evaluate`, `LegacyProjectTests.fs` (overlay extracted
from the docs' xml block), release notes, `build test pack`, the Windows acceptance kit on the VM.
