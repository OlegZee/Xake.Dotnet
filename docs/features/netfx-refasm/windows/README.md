# Windows acceptance: .NET Framework reference assemblies from the package

`RecordOptions.NetFxReferenceAssemblies` on a Windows machine with the .NET Framework 4.7.2
targeting pack installed. Not run in CI (no runner has the pack); `NetFxReferenceTests.fs` covers
the rest on every OS.

`fixture/`: `fx` (`net472;netstandard2.0`, `Reference System.Management` from the package and
`System.Management.Automation`, in a GAC only), `cpm` (`net472` under central package management,
`PackageReference Newtonsoft.Json` without a version), `global.json` pinned to SDK 10.0.404, and
`accept.fsx` with the commands `package`, `installed` and `build`. `nuget.config` makes `pkg/` a
source: put the package under test there (`dotnet fsi build.fsx -- -- pack -d VERSION=4.4.0-netfx.1`,
the version `accept.fsx` names).

Run (from macOS, VM `win11`):

```bash
cp out/pkg/Xake.Dotnet.4.4.0-netfx.1.nupkg docs/features/netfx-refasm/windows/fixture/pkg/
cd docs/features/netfx-refasm/windows && COPYFILE_DISABLE=1 tar -czf /tmp/accept.tgz --exclude obj --exclude locks fixture accept.ps1
scp /tmp/accept.tgz win11:C:/probe-netfx/ && ssh win11 'cd C:\probe-netfx; tar -xzf accept.tgz; powershell -NoProfile -ExecutionPolicy Bypass -File C:\probe-netfx\accept.ps1 C:\probe-netfx\fixture'
```

Expected: `RESULT package=True build=True installed-fails=True`; the `package` locks name
`pkg:microsoft.netframework.referenceassemblies.net472/1.0.3/...` and no `Reference Assemblies`;
`installed` fails naming `C:/Program Files (x86)/Reference Assemblies/.../mscorlib.dll`.
Then compare with macOS: `dotnet fsi accept.fsx -- -- package` in a copy of `fixture/` outside a
git checkout (inside one, `AssemblyInformationalVersion` gets `+$(SourceRevisionId)`) and on a
path without `--`; `cmp` each `locks/<name>/Release.json` with the VM's.

## Run on 2026-11-19 (package `4.4.0-netfx.1`, VM with packs v4.0 to v4.8.1)

- SDK 10.0.404: `package` and `build` pass, `installed` fails as expected; `locks/fx` and
  `locks/cpm` byte-identical to the macOS locks (SDK 10.0.404).
- SDK 8.0.428 (`global.json` changed on the VM): the same three results.
