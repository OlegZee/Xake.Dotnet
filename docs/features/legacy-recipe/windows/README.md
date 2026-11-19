# Windows acceptance: the legacy-project overlay

The overlay of [docs/legacy-projects.md](../../../legacy-projects.md), used as is, on a Windows
machine with the .NET Framework 4.7.2 targeting pack installed (where the import must still take
the reference assemblies from the package). `LegacyProjectTests.fs` covers the same on every OS
(CI runs it on Linux and Windows); this run adds the packed package and a machine with the packs.

`fixture/`: `legacy/legacy.csproj` (old format, `TargetFrameworkVersion v4.7.2`, `PackageReference
Newtonsoft.Json`, a XAML `Page` with code-behind), `global.json` pinned to SDK 10.0.404, and
`accept.fsx` with the commands `bare` (no overlay), `record` (the overlay taken from
`legacy-projects.md` beside it) and `build`. `nuget.config` makes `pkg/` a source: put the package
under test there (`dotnet fsi build.fsx -- -- pack -d VERSION=4.4.0-legacy.1`, the version
`accept.fsx` names).

Run (from macOS, VM `win11`):

```bash
cp out/pkg/Xake.Dotnet.4.4.0-legacy.1.nupkg docs/features/legacy-recipe/windows/fixture/pkg/
cp docs/legacy-projects.md docs/features/legacy-recipe/windows/fixture/
cd docs/features/legacy-recipe/windows && COPYFILE_DISABLE=1 tar -czf /tmp/accept-legacy.tgz --exclude obj --exclude locks fixture accept.ps1
scp /tmp/accept-legacy.tgz win11:C:/probe-legacy/ && ssh win11 'cd C:\probe-legacy; tar -xzf accept-legacy.tgz; powershell -NoProfile -ExecutionPolicy Bypass -File C:\probe-legacy\accept.ps1 C:\probe-legacy\fixture'
```

Expected: `RESULT bare-guard=True record=True lock=True build=True`: without the overlay the import
fails with the guard naming A, B and D; with it the lock names
`pkg:microsoft.netframework.referenceassemblies.net472/1.0.3/...`, `pkg:newtonsoft.json/13.0.3/...`
and the recorded `Legacy.g.resources`, and no `Reference Assemblies`; the lock builds.

## Run on 2026-11-19 (package `4.4.0-legacy.1`, VM with packs v4.0 to v4.8.1)

- SDK 10.0.404 and 8.0.428: `RESULT bare-guard=True record=True lock=True build=True` (the guard
  names A, B and D; `pkg:` reference assemblies, Newtonsoft and `Legacy.g.resources` in the lock,
  no `Reference Assemblies`; the lock builds).
