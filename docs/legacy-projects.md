# Legacy (non-SDK) projects and `Import.record`

`Import.record` (and `Import.project`) work on SDK-style projects (`<Project Sdk="Microsoft.NET.Sdk">`).
A **legacy** project is the older format: `<Project ToolsVersion="...">` with no `Sdk`, importing
`Microsoft.Common.props` and `Microsoft.CSharp.targets` itself, with `TargetFrameworkVersion`
(`v4.7.2`) instead of `TargetFramework` (`net472`). Visual Studio extensions are still often
written this way.

Xake.Dotnet does not support such a project natively. The import runs .NET's MSBuild
(`dotnet msbuild`), and there a legacy project lacks what the import relies on:

| Section | What the import needs | Why a legacy project lacks it |
|---|---|---|
| A. Framework | `TargetFramework` (`net472`) | the import keys every entry by its framework; a legacy project names only `TargetFrameworkIdentifier` + `TargetFrameworkVersion` |
| B. Package resolution | `PackageReference` assets turned into references; the SDK's version and folder | `Microsoft.NuGet.targets`, which resolves them for a legacy project, ships only with Visual Studio's MSBuild; `NETCoreSdkVersion` comes from a file only SDK projects import, and without it the lock cannot name the compiler (`sdk:`) |
| C. XAML | the markup compiler for `Page` items | wired into a legacy project by Visual Studio's MSBuild only |
| D. Reference assemblies | `Microsoft.NETFramework.ReferenceAssemblies` | the SDK adds this package to an SDK project (and the import makes it do so on every OS, [hermetic/lock.md](hermetic/lock.md#identities)); a legacy project does not run that SDK target, so on macOS/Linux there are none (MSB3644) and on Windows the installed `Program Files` packs, which the import refuses |

The import overlay below supplies all four. It is a targets file the import hands every project as
`CustomAfterMicrosoftCommonTargets`; everything in it is off for SDK projects.

## The overlay

Copy it as is into the repository (here `build/legacy.targets`). Drop section C when no project has
XAML `Page` items: on a project without them it does no harm, but adds `Version` (`1.0.0.0`) to the
lock's properties.

```xml
<Project>
  <!-- Import overlay for legacy (non-SDK) C# projects: pass it to Import.record as the property
       CustomAfterMicrosoftCommonTargets. Everything here is off for SDK projects. -->

  <!-- A. Framework: the TFM the import keys entries by (.NETFramework v4.7.2 -> net472) -->
  <PropertyGroup Condition="'$(UsingMicrosoftNETSdk)' != 'true' and '$(TargetFramework)' == '' and '$(TargetFrameworkIdentifier)' == '.NETFramework'">
    <TargetFramework>net$(TargetFrameworkVersion.TrimStart('vV').Replace('.', ''))</TargetFramework>
  </PropertyGroup>

  <!-- B. PackageReference assets resolved by the .NET SDK, as in an SDK project (Visual Studio's
       MSBuild has Microsoft.NuGet.targets for this; dotnet msbuild does not) -->
  <PropertyGroup Condition="'$(UsingMicrosoftNETSdk)' != 'true' and '$(MSBuildRuntimeType)' == 'Core'">
    <_XakeLegacy>true</_XakeLegacy>
    <_DotNetAppHostExecutableNameWithoutExtension>apphost</_DotNetAppHostExecutableNameWithoutExtension>
  </PropertyGroup>
  <Import Project="$(MSBuildExtensionsPath)/Microsoft.NETCoreSdk.BundledVersions.props" Condition="'$(_XakeLegacy)' == 'true'" />
  <Import Project="$(MSBuildSDKsPath)/Microsoft.NET.Sdk/targets/Microsoft.NET.Sdk.Common.targets" Condition="'$(_XakeLegacy)' == 'true'" />
  <Import Project="$(MSBuildSDKsPath)/Microsoft.NET.Sdk/targets/Microsoft.PackageDependencyResolution.targets" Condition="'$(_XakeLegacy)' == 'true'" />
  <!-- what ResolvePackageAssets depends on and a legacy project lacks (.NET Core frameworks,
       publish); an SDK project defines the real ones after this file, so they win there -->
  <Target Name="ProcessFrameworkReferences" />
  <Target Name="_DefaultMicrosoftNETPlatformLibrary" />
  <Target Name="_ComputePackageReferencePublish" />
  <Target Name="ResolveRuntimePackAssets" />

  <!-- C. XAML Page items: the markup compiler (drop this section where no project has XAML) -->
  <PropertyGroup Condition="'$(_XakeLegacy)' == 'true'">
    <UseWPF>true</UseWPF>
  </PropertyGroup>
  <Import Project="$(MSBuildSDKsPath)/Microsoft.NET.Sdk.WindowsDesktop/targets/Microsoft.WinFX.targets" Condition="'$(_XakeLegacy)' == 'true'" />

  <!-- D. Reference assemblies from the package, as the SDK adds it to an SDK project; the switch
       and the version are the import's (RecordOptions.NetFxReferenceAssemblies*) -->
  <ItemGroup Condition="'$(UsingMicrosoftNETSdk)' != 'true' and '$(TargetFrameworkIdentifier)' == '.NETFramework' and '$(AutomaticallyUseReferenceAssemblyPackages)' == 'true'">
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="$(MicrosoftNETFrameworkReferenceAssembliesLatestPackageVersion)" PrivateAssets="all" IsImplicitlyDefined="true" />
  </ItemGroup>
</Project>
```

Notes on the sections:

- **A** takes the framework the project already declares; a project that sets `TargetFramework`
  itself keeps it.
- **B** is conditioned on `MSBuildRuntimeType` = `Core` (.NET's MSBuild), not on
  `Microsoft.NuGet.targets` being absent: that file is imported after this overlay, so the overlay
  cannot see it. The import always runs `dotnet msbuild`. The restore targets need no import:
  `Microsoft.Common.targets` imports `NuGet.targets` in every project.
- **C** sets `UseWPF`: the import compiles XAML in a run of its own for projects with `UseWPF` and
  `Page`/`ApplicationDefinition` items ([hermetic/lock.md](hermetic/lock.md)), and a legacy WPF
  project does not set it. The markup compiler runs on macOS and Linux too.
- **D** reads the import's own properties: `AutomaticallyUseReferenceAssemblyPackages=true` and
  the version `RecordOptions.NetFxReferenceAssembliesVersion` under
  `NetFxReferenceAssemblies.Package` (the default). Under `NetFxReferenceAssemblies.Installed`
  the import sets neither and the overlay adds nothing: the installed packs, as for SDK projects.
  `IsImplicitlyDefined` keeps central package management from asking for a version (NU1008).
  A plain `dotnet build` with the overlay passes the two properties itself to get the package.

## Passing it

```fsharp
let record =
    { Import.RecordOptions.Default with
        Properties = [ "CustomAfterMicrosoftCommonTargets", __SOURCE_DIRECTORY__ + "/build/legacy.targets" ] }
Import.record record [ "src/Ext/Ext.csproj"; "src/Lib/Lib.csproj" ]
```

The path is absolute: msbuild resolves a relative one against the folder of its own targets. The
import chains the file from its own targets file (and records it `$(ProjectRoot)`-relative). It reaches every project of the import, SDK ones included, which it leaves alone.
The overlay is an import input: every lock records it in `Evaluation.Imports`, so an edit makes
the locks stale ("run the import again"), as an edit of the project file does.

## Without the overlay

The import fails the project after its evaluation, before any build:

```text
'<project>': legacy (non-SDK) project; import needs TargetFramework and PackageReference resolution — see docs/legacy-projects.md (missing: A framework, B package resolution, D reference assemblies)
```

`missing` names the sections the evaluation shows absent: A when there is no
`TargetFramework(s)`, B when the SDK's package resolution is not imported (`NETCoreSdkVersion` or
`ProjectAssetsCacheFile` empty), D when the project targets .NET Framework, the import wants the
reference assemblies package and the project has no `PackageReference` to it. C is not checked:
a project without XAML does not need it, and one with XAML without C fails in the markup run. An
SDK project never fails this way. Before this release the import stopped at the same point with
`msbuild reports no TargetFramework(s)`; with a partial overlay it failed later, with MSB3644 or
"the compiler ... cannot be named portably".

## Limits

The recipe covers what it was tried on: C# libraries and applications on .NET Framework with
`Reference`, `PackageReference` and XAML `Page` items, recorded on macOS, Linux and Windows. Out:
`packages.config` (convert to `PackageReference`), VB, C++/CLI, COM references, and anything else
only Visual Studio's MSBuild builds (VSIX packaging, VSCT compilation: build those outside the lock).
