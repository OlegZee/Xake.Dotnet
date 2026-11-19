# Session: netfx-refasm

Branch `feature/netfx-refasm` from `dev` at `cc6e3de`. Task B-5: the import takes .NET Framework
reference assemblies from `Microsoft.NETFramework.ReferenceAssemblies` on every OS. Goal:
`brief.md` (written in the consumer's repository, code references to v4.3.0). PR #12
(`feature/import-file-casing`) also touches `Import.fs`: do not touch it.

## State at 2026-11-19

Probed and designed; no code. Findings that correct the brief:

- The SDK's probe (`IncludeTargetingPackReference`, `Microsoft.NET.Sdk.FrameworkReferenceResolution.targets`,
  same in SDK 8 and 11) reads `TargetFrameworkRootPath` (and `TargetFrameworkFallbackSearchPaths`),
  not `FrameworkPathOverride`.
- `TargetFrameworkRootPath` must **not** be a global property: the package's own
  `build/*.targets` sets it to the package, and a global wins, giving MSB3644 (an error on Windows,
  a warning and no references on macOS; seen on SDK 8.0.428, 9.0.320, 10.0.404). It is set in the
  import's props file (`restoreProps`, before the package's targets), for `.NETFramework` inner
  builds only.
- `MicrosoftNETFrameworkReferenceAssembliesLatestPackageVersion` is set unconditionally in
  `Microsoft.NET.Sdk.props` after `Directory.Build.props`: it must be global.
- `System.Management` is in the 4.7.2 package. A GAC-only assembly (`System.Management.Automation`)
  is MSB3245 (warning) on Windows and macOS, with the root hidden or not: `dotnet msbuild` does not
  search the GAC. Only with `BuildOnlySettings` (as the import runs) is the warning printed.
- The implicit reference (`IsImplicitlyDefined`) gives no NU1008 under central package management
  (Windows and macOS).
- Staleness checks compare file hashes only: no `RecordOptions` value makes a lock stale today.
  Decision D1 for the owner (design note in the orchestrator's notes folder,
  `netfx-refasm-design-2026-11-19.md`).

Probe fixture and the VM run: scratchpad of this session (not kept); VM folder `C:\probe-netfx`.

## Next step

Wait for the owner's approval and D1. Then: docs, `Import.fs` (+25 lines), tests, the Windows
acceptance script in `windows/` here, release notes "Unreleased", `build test pack`.
