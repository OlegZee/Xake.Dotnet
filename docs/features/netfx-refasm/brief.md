# Brief: .NET Framework reference assemblies from the package on every OS (Xake.Dotnet, after 4.3.0)

Audience: the agent working in the Xake.Dotnet repository. Xake.Dotnet is public: this brief names no product or
company of the consumer. Code references are to v4.3.0 (`src/hermetic`, commit `b4853da`).

## Problem

An SDK project targeting `net472` resolves its reference assemblies differently by OS:

- macOS/Linux: none installed, so the SDK adds the `Microsoft.NETFramework.ReferenceAssemblies` package
  (`AutomaticallyUseReferenceAssemblyPackages`), and the lock names `pkg:` files. Portable.
- Windows with a targeting pack installed: the SDK takes
  `C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2\…`, and import rejects these
  as non-portable host files. The import fails (or, if allowed, the lock would depend on the machine).

So the same project gives different locks, or no lock, depending on where import runs.

The consumer works around it with an import overlay that adds a `PackageReference` to
`Microsoft.NETFramework.ReferenceAssemblies`, and only for some projects: projects with central package management
fail with NU1008 when a reference carries an explicit version. Import of the rest on Windows is untested and most
likely fails the same way.

## What to do

Make import always take the package, without touching projects:

1. Pass global properties at import evaluation:
   - `AutomaticallyUseReferenceAssemblyPackages=true`;
   - hide installed reference assemblies so the SDK's "already installed?" probe says no: point
     `TargetFrameworkRootPath` (and, if needed, `FrameworkPathOverride`) at an empty directory under import's temp
     folder. Check in the SDK (`Microsoft.NETFramework.ReferenceAssemblies.targets`,
     `Microsoft.NET.TargetFrameworkInference.targets`) which property the probe really reads, and set only that one;
   - `MicrosoftNETFrameworkReferenceAssembliesLatestPackageVersion=<v>` from a new `RecordOptions` setting (default
     `1.0.3`), so the version is fixed by import, not by the SDK release.
   No `PackageReference` is added: the SDK's implicit reference is used, so central package management is not
   involved (verify with a CPM fixture).
2. Apply to every TFM of the .NET Framework family (`net4*`). Leave other TFMs alone.
3. References that are not in the reference package (e.g. `System.Management` resolved from the GAC on Windows,
   MSB3245 on other OS) must behave the same on every OS: unresolved → warning, not a host path in the lock. Check
   that hiding the root does not turn these into a different failure.
4. An option to turn this off (`RecordOptions.NetFxReferenceAssemblies = Package | Installed`, default `Package`)
   for consumers that do want installed packs; `Installed` keeps today's behaviour.

## Acceptance

- A `net472` SDK project imported on Windows with the 4.7.2 targeting pack installed records `pkg:` reference
  assemblies; the lock equals the one recorded on macOS (same `Dependencies.Packages`, same csc switches).
- A `net472` project with central package management imports on Windows and macOS without NU1008.
- Projects not targeting .NET Framework: locks byte-for-byte unchanged.
- ~~Changing the configured package version makes existing locks stale through the normal import-input check.~~
  Replaced by decision D1 (a), below.
- Consumer side (not this repository): the overlay section that adds the package is removed; a re-import on Windows
  gives the same locks as on macOS.

## Decisions (owner, 2026-11-19, after the probe)

- **How the installed packs are hidden.** `TargetFrameworkRootPath` is *not* a global property: the
  package's own `build/*.targets` sets it to the package after the restore, and a global property
  would win over that (MSB3644, no references; seen on SDK 8.0.428, 9.0.320, 10.0.404). It is an
  ordinary property of the import's props file, for `.NETFramework` frameworks and where the
  framework is not known yet (a single-framework project's restore). The version and
  `AutomaticallyUseReferenceAssemblyPackages` stay global (the SDK sets the version after
  `Directory.Build.props`). No explicit `PackageReference` from the import (the SDK's implicit one).
- **D1 (a): a changed version is treated like `PacksFrom`.** The next import rewrites the `net4*`
  locks and a re-import comparison (`check-locks`) catches a committed lock with another version;
  no version is recorded in the lock as such and there is no extra staleness check (the normal
  check compares the hashes of files, and a `RecordOptions` value is not a file). This replaces
  the acceptance line on staleness above.
- `System.Management` is in the 4.7.2 package; the example of an assembly the package lacks is
  `System.Management.Automation` (in a GAC only): MSB3245 on every OS, since `dotnet msbuild`
  does not search the GAC.
