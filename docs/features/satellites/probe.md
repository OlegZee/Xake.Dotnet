# Probe: the satellite compile under the record (2026-11-07)

A C# library (`net8.0`, `Version` 1.2.3, `FileVersion` 1.2.3.4, `Company`, `Product`,
`Copyright`, `Deterministic`) with `Strings.resx`, `Strings.ja.resx`, `Strings.zh-CN.resx`, on
SDK 8.0.427 and 10.0.403 (macOS). The same results on both SDKs unless noted.

## A. The record's targets as they are

`BuildOnlySettings;PrepareForBuild;ResolveReferences;PrepareResources;ResolveKeySource;Compile`
with `ProvideCommandLineArgs`, `SkipCompilerExecution`, `BuildProjectReferences=false`
(`Import.designTimeTargets`): succeeds; no satellite target runs. `PrepareResources` writes
every `.resources`, the culture ones included (`obj/.../Lib.Strings.ja.resources`,
`Lib.Strings.zh-CN.resources`), so the satellite's inputs exist after the record.

## B. The same plus `CreateSatelliteAssemblies`

Fails: `error MSB4018: The "GetAssemblyAttributes" task failed unexpectedly.
System.IO.FileNotFoundException: .../obj/Debug/net8.0/Lib.dll` (`_GetAssemblyInfoFromTemplateFile`,
`Microsoft.NET.Sdk.targets` line 1127 on SDK 8, 1200 on SDK 10). Even without that failure the
satellite's `Csc` could not be captured: unlike `CoreCompile` it passes neither
`ProvideCommandLineArgs` nor `SkipCompilerExecution`, so it would compile for real and report no
command line.

## C. A full build

The satellite's command line (SDK 8 runs `dotnet exec csc.dll`, SDK 10 the `csc` apphost; the
switches are the same):

```
csc /noconfig /nostdlib+ /warn:8 /reference:<every ReferencePath> /optimize-
    /out:obj/Debug/net8.0/ja/Lib.resources.dll /resource:obj/Debug/net8.0/Lib.Strings.ja.resources
    /target:library /deterministic+ obj/Debug/net8.0/ja/Lib.resources.cs /warnaserror+:NU1605,SYSLIB0011
```

No `/define`, analyzers, `/debug`, `/langversion`, `/nullable`. Key switches come from
`KeyOriginatorFile`/`KeyContainerName`, `PublicSign`, `DelaySign` (not exercised here).

`ja/Lib.resources.cs` holds Company, Copyright, FileVersion, InformationalVersion, Product,
Title, Version and `AssemblyCulture("ja")`. With generated assembly info it equals the main
`Lib.AssemblyInfo.cs` without `AssemblyConfiguration`, plus `AssemblyCulture`, line for line.
A satellite compiled by hand from that derived source, with the switches above, is
**byte-identical** to msbuild's (SDK 10; `/out` must keep the name `Lib.resources.dll`, since the
module name is part of the image).

## D. Hand-written attributes (`GenerateAssemblyInfo=false`)

`AssemblyInfo.cs` with `AssemblyVersion("2.0.0.0")`, `AssemblyCompany("HandCo")`,
`AssemblyDescription(...)`. The satellite's source then has Company "HandCo", Copyright `" "`,
Description, InformationalVersion "2.0.0.0", Title `" "`, Version "2.0.0.0", no FileVersion.
`GetAssemblyAttributes` reads the **file version info** of the built dll (`FileVersionInfo`),
not the source attributes: the blanks are the version resource's defaults. There is no
`*.AssemblyInfo.cs` to derive from, so deriving from the main entry's `Generated` text covers
only projects with generated assembly info.

## E. Signing (2026-11-07)

The same library with `SignAssembly`, `AssemblyOriginatorKeyFile` (a 1024-bit test key) in
three modes, SDK 8.0.427 and 10.0.403, a full build:

| mode | main `csc` key switches | satellite `csc` key switches | tokens |
|---|---|---|---|
| key file | `/keyfile:<key>` | `/keyfile:<key>` | equal |
| `PublicSign` | `/keyfile:<key> /publicsign+` | `/keyfile:<key> /publicsign+` | equal |
| `DelaySign` | `/delaysign+ /keyfile:<key>` | `/delaysign+ /keyfile:<key>` | equal |

The satellite's key switches are the main compile's, as the target's recipe says
(`KeyOriginatorFile`, `PublicSign`, `DelaySign`). Version 1.2.3.0 and culture `ja` as expected.
A satellite composed by hand with the key file (the derived source, the switches of C plus
`/keyfile`) is **byte-identical** to msbuild's signed one (SDK 10; real signing is
deterministic). `KeyContainerName` not probed (Windows only).

## F. Which attributes (2026-11-07)

A project with `Description`, `AssemblyTitle`, `NeutralLanguage`, `Authors`, a version with a
suffix, `InternalsVisibleTo`, `AssemblyMetadata` and an `AssemblyTrademark` attribute (SDK 10):
the satellite takes Company, Copyright, Description, FileVersion, InformationalVersion, Product,
Title, Version, in that order, and nothing else (no Trademark, Configuration,
`NeutralResourcesLanguage`, `InternalsVisibleTo`, `AssemblyMetadata`). A copyright the assembly
does not state reads as `" "`; a description it does not state is left out. This is the file
version info `GetAssemblyAttributes` reads (`Import.satelliteAttributesOf` follows it).

## Consequences

1. The satellite's `Csc` call cannot be captured by the design-time record (B). Its switches are
   few and fixed in the SDK's target; the variable parts are the references (the main entry's),
   the key switches (the main entry's), `Optimize`, `WarningLevel`, `Deterministic`,
   `WarningsAsErrors`/`NotAsErrors`, `DisabledWarnings` (properties the record can read).
2. The attributes have two possible sources:
   - **from the main entry's `Generated` assembly info** (minus `AssemblyConfiguration`, plus
     `AssemblyCulture`): byte-identical for generated assembly info (C); wrong for hand-written
     attributes (D);
   - **from the built main assembly at build time**, as msbuild does: read its file version info
     after the assembly rule, then write the source. Faithful in both cases, but `FileVersionInfo`
     is platform-specific (a Win32 resource on Windows, metadata elsewhere): the blanks seen in D
     may differ on Windows (not probed).
3. Decision 1 of the brief ("capture msbuild's satellite `Csc` call") cannot hold as stated; it
   becomes "record the satellite's inputs and settings, compose the `Csc` call by the SDK target's
   recipe". This needs the owner's answer.

Not probed: Windows (`FileVersionInfo`, key containers), `net472` (the `al`-free route of
`dotnet msbuild` assumed), F#.

## G. Windows and .NET Framework (2026-11-08)

Windows 11 (ARM64), SDK 8.0.301 and 9.0.304 (9.0.304's satellite target already filters the
references to `mscorlib`, `netstandard`, `System.Runtime`, as SDK 10's does), against macOS SDK
8.0.427 and 10.0.403. Three signed libraries (`net8.0;net472`, `Strings.resx` with `ja` and
`zh-CN`) and a `net472` console application referencing them:

- **generated** assembly info: Version `1.2.3-beta.1`, FileVersion, Company, Product with `ü` and
  `"`, Copyright, Description;
- **hand-written**: `AssemblyVersion("2.0.0.0")`, Company, Description, no FileVersion;
- **empty**: `AssemblyVersion("3.0.0.0")`, `AssemblyFileVersion("3.1.0.0")`, and Company,
  Product, Copyright, Title, InformationalVersion all `""`.

**Attributes** (`obj/.../<culture>/<asm>.resources.cs`, compared as attribute lines): the same on
both SDKs of an OS, and the same between macOS and Windows except one line: for **hand-written
attributes without `AssemblyFileVersion`**, Windows adds `AssemblyFileVersion("2.0.0.0")` (the
assembly version, from csc's Win32 version resource), macOS leaves it out. Generated assembly info
and the empty case are identical. Further rules seen on both OS:

| attribute in the main assembly | satellite |
|---|---|
| Copyright / Title absent | `" "` |
| InformationalVersion absent | the assembly version |
| FileVersion absent | left out (macOS); the assembly version (Windows) |
| Description absent | left out |
| any of Company, Product, Copyright, Title, InformationalVersion given as `""` | left out (not `" "`, not the version) |
| Title not set in a generated-info project | the assembly name (the SDK generates it) |

**Loading on .NET Framework 4.8** (`App472.exe` built on macOS with SDK 10, i.e. the filtered
references and the metadata-read attributes, copied to Windows): `ResourceManager` finds `ja`
and `zh-CN` for all three libraries; the loaded satellites are `<asm>.resources, Version=<the
main assembly's>, Culture=<c>, PublicKeyToken=<the main's>`. The same app built on Windows
(SDK 9) behaves the same. **A satellite with another token** (built unsigned, put in place of the
signed `ja` one): no error, `ResourceManager` silently falls back to the neutral string; only
that satellite is not loaded.

Consequences: decision 16 holds as stated (verified, not inferred); the reader's rule table
(decision 11) gains the empty-string row; decision 18's check is what prevents a silent fallback.
Not probed: key containers, Visual Studio's msbuild (`al.exe`).
