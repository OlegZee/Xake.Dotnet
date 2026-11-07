# Brief: satellites (S71)

Build satellite assemblies (`<out>/<culture>/<AssemblyName>.resources.dll`) for the
culture-specific resources of a project (`Strings.de.resx`, `Logo.fr-FR.png`), and place a
referenced project's satellites in an application's run folder, so that a localized
application builds from locks like any other.

Reopens S71 ("Satellite assemblies: deferred by decision", `docs/hermetic/README.md`). The
analysis is in `docs/features/hermetic-windows/analysis/lock-content.md` §3; the owner's
answers in `docs/features/hermetic-windows/interview.md`, round 4.

## Today

- **Imported locks** (`src/hermetic/Import.fs`): an `EmbeddedResource` with `WithCulture` (or a
  culture suffix msbuild would see) is dropped with a report ("a satellite assembly, not
  built"); the resx pairs of the main assembly skip culture items.
- **Composed path** (`src/dotnet/ProjectPlan.fs`, `cultureOf`): a culture-specific resource in a
  description fails the build ("satellite assemblies are not built by the composed path").
- **Run folders**: only the `Related` files beside a referenced assembly are copied, not its
  culture subfolders; `deps.json` has no `"resources"` lines for them.
- **Package satellites already work**: copy-local items with a `DestinationSubDirectory`
  (`App.Copy`), listed by SBOM tier 1, taken by `Pack` with a wildcard per folder.
- Docs listing satellites as not covered: `docs/hermetic/README.md`, `lock.md`, `pack.md`,
  `description.md`, `docs/description.md`.

## Decided (interview round 4, 2026-10-26)

1. ~~Capture msbuild's satellite `Csc` call, do not compose it~~: superseded by 4 below (the
   probe showed the call cannot be captured).
2. A culture-suffixed resx under invariant globalization fails the import (the same project
   would otherwise give a different lock).
3. C# first; F# when a consumer needs it.
4. **Compose the satellite's `Csc` call by the SDK target's recipe** (`CoreGenerateSatelliteAssemblies`),
   from inputs and settings the record reads (owner, 2026-11-07, after `probe.md`). The
   attributes come from the main entry's `Generated` assembly info (without
   `AssemblyConfiguration`, plus `AssemblyCulture`) when the project generates assembly info,
   else from the built main assembly at build time, as msbuild's `GetAssemblyAttributes` does.
5. The composed path (`xake.project.json`, `ProjectPlan`) keeps its refusal of culture-specific
   resources (owner, 2026-11-07).
6. Scope (owner, 2026-11-07): a project's own satellites, built **by a separate rule the script
   requests explicitly**; building the main assembly does not build them. The import records
   their inputs always (a lock without culture resources is unchanged). A referenced project's
   satellites in a run folder and the `deps.json` `"resources"` lines: **not now**.
7. Release: 4.3.0. `feature/import-file-casing` (#12) is not touched by this feature (owner,
   2026-11-07).

## Shape (option A of the analysis, adjusted by decision 4)

- Per culture in the entry: the culture resx -> `.resources` pairs (compiled by the existing
  `Resx`) with their manifest names, and the settings the target reads (`Optimize`,
  `WarningLevel`, `Deterministic`, warnings as errors, disabled warnings; the key switches are the
  main entry's). Compiler and references are the main entry's; the switches are the target's
  fixed set (`probe.md`, C).
- The satellite rule (decision 6) writes `<out>/<culture>/<asm>.resources.dll`;
  `AssemblyVersion` and the public key token equal the main assembly's.
- Later (not now, decision 6): run folders take each referenced lock's satellites by name from
  the lock; `deps.json` `"resources"` lines from stand-in satellites in the app pass.
- Signing takes satellites like any other built file; `Pack` and the SBOM pick them up.
- Lock format: a new optional field, no bump; a lock without culture resources is
  byte-identical to today's.

## Open decisions

(none)

## Risks

- The `Csc` parameters of `CoreGenerateSatelliteAssemblies` differ between SDK 8 and 9/10:
  capture, never guess.
- **The satellite's attributes come from the built main assembly.** `CoreGenerateSatelliteAssemblies`
  depends on `_GetAssemblyInfoFromTemplateFile`: `GetAssemblyAttributes
  PathToTemplateFile="$(IntermediateOutputPath)$(TargetName)$(TargetExt)"` reads Company,
  Copyright, FileVersion, InformationalVersion, Product, Title, Version from the compiled dll;
  then `WriteCodeFragment` writes `<culture>/<asm>.resources.cs` with them and `AssemblyCulture`
  (`Microsoft.NET.Sdk.targets`, the same in SDK 8.0.427 and 10.0.403). Under the record's
  `SkipCompilerExecution=true` the dll does not exist, so the captured source would lack them
  (or the target fails; not probed). Likely way out: capture the satellite's `Csc` switches only and
  derive the attributes from the main entry's `Generated` `*.AssemblyInfo.cs` (the same set, the
  version already a token) plus `AssemblyCulture`. Seen in a probe (SDK 8.0.427, `dotnet build
  -v:d`), the satellite's command line: `/noconfig /nostdlib+ /warn /reference:@(ReferencePath)
  /keyfile /optimize /out:obj/.../<culture>/<asm>.resources.dll
  /resource:<ns>.<name>.<culture>.resources /target:library /deterministic+ <cs>`: no `/define`,
  analyzers or `/debug`. This revisits decision 1 (capture vs compose) for the attributes only.
  **Probed** (`probe.md`, 2026-11-07): adding `CreateSatelliteAssemblies` to the record fails
  (MSB4018, no main dll), and the satellite's `Csc` takes neither `ProvideCommandLineArgs` nor
  `SkipCompilerExecution`, so it cannot be captured at all. Composing it from the derived source
  gives a byte-identical satellite for generated assembly info; hand-written attributes need
  the built main assembly's file version info.
- Signing parity, the version substitution (`VersionToken`), run-folder conflicts in culture
  subfolders, ICU vs NLS culture lists at the edges.
