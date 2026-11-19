# Windows acceptance: the source order does not depend on the importing OS

`fixture/globs`: a C# project whose glob-expanded files sort differently with `\` and `/`
(`Lower2.cs`/`lower/m.cs`, `ModelsExt.cs`/`Models/A.cs`, ...), with `EmbeddedResource` and
`AdditionalFiles` globs (`Data/`, `DataX/`, `Data_1.txt`). `global.json` takes SDK 8.0.4xx.
`nuget.config` makes `pkg/` a source: put the package under test there
(`dotnet fsi build.fsx -- -- pack -d VERSION=4.4.0-srcorder.1`, the version `accept.fsx` names).

Run (from macOS, VM `win11`):

```bash
cp out/pkg/Xake.Dotnet.4.4.0-srcorder.1.nupkg docs/features/source-order/windows/fixture/pkg/
cd docs/features/source-order/windows && COPYFILE_DISABLE=1 tar -czf /tmp/accept.tgz --exclude obj --exclude locks --exclude out fixture accept.ps1
scp /tmp/accept.tgz win11:C:/probe-srcorder/ && ssh win11 'cd C:\probe-srcorder; tar -xzf accept.tgz; powershell -NoProfile -ExecutionPolicy Bypass -File C:\probe-srcorder\accept.ps1 C:\probe-srcorder\fixture'
```

Then on macOS, in a copy of `fixture/` outside a git checkout: `dotnet fsi accept.fsx -- -- record`
and `-- build`; compare `locks/globs/Release.json` (and the dll) with the VM's.

## Run on 2026-11-19 (package `4.4.0-srcorder.1`; macOS SDK 8.0.427, VM SDK 8.0.428)

- `record` and `build` pass on both. `locks/globs/Release.json` equal to the macOS one once the
  SDK patch is normalised (8.0.427/8.0.428, packs 8.0.33/8.0.34: the `Sdk`, the analyzer config
  path, the reference hashes, the ref pack's `Sha512`): `Sources`, `Placed` (the two generated
  files only), `/resource:` and `/additionalfile:` in the `/` order. The dlls differ (other
  reference packs), so they are not compared.
- Control, the same fixture on the VM with `4.4.0-preview.1`: `Placed` also names `Lower2.cs`
  and `ModelsExt.cs`, and `DataX/c.txt` comes first in both item switch blocks -- the reported
  difference.
