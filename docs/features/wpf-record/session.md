# Session: wpf-record

Branch `feature/wpf-record` from `dev` at `v4.0.0`. Goal and design: `brief.md`; items:
`tracker.md`.

## State at 2026-11-05

- Implemented in `src/hermetic/Import.fs` (`record`: `markupProjects`, the markup run after the
  design-time traversal of the other projects; `referencedProject`; `projectsOf` accepts a
  project whose lock exists; `entryOf`: `markupSources`, `.resx`-only resources; `restoreProps`:
  `_wpftmp`). Tests in `src/hermetic.tests/RecordTests.fs` (`Import record WPF`, its own
  folder: `RecordKit.build` takes every description of the folder).
- Packed locally as `4.1.0-wpf.2` (`dotnet fsi build.fsx -- -- pack -d VERSION=4.1.0-wpf.2`);
  a consumer script took it through `#i "nuget: <out/pkg>"`: two brands recorded (the WPF
  projects after building the others), built, packed with obfuscation; a second import
  changed no file.
- Traps found on the way: WinFX hands the `_wpftmp` project the parent's
  `MSBuildProjectExtensionsPath` but copies its NuGet props into `BaseIntermediateOutputPath`;
  the referenced projects' reference assemblies (`obj/.../ref/`) are not built, hence
  `ProduceReferenceAssembly=false`; with `SkipCompilerExecution=true` pass two fails (MC3074)
  even with the references in place.

## State at 2026-11-06

- Referenced identities: recorded and verified (brief, "Open decisions"). `Baml.ofResources`
  reads the `AssemblyInfo` records of the recorded `g.resources`; those of referenced projects go
  into `Recorded.Markup`; `Locks.staleMarkup` compares them with the built outputs. Full suite
  green (478), fsc `build check` green. Packed as `4.1.0-wpf.3`; a consumer's re-import changed
  no lock (its XAML names no referenced project, so no `Markup` key).

## Next step

The PR to `dev` and the 4.1.0 release. Open: a WPF window from a consumer's output on Windows.
