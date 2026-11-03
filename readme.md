# Xake.Dotnet

The .NET tasks and reproducible builds for [Xake](https://github.com/OlegZee/Xake), a build
utility that uses the full power of F#.

[![Build and Test](https://github.com/OlegZee/Xake.Dotnet/actions/workflows/build.yml/badge.svg)](https://github.com/OlegZee/Xake.Dotnet/actions/workflows/build.yml)

**From Xake 4.0 the .NET tasks are a package of their own.** A script that compiles with `csc`
or `fsc` adds one line; it brings the engine too:

```fsharp
#r "nuget: Xake.Dotnet"

open Xake
open Xake.Dotnet

do xakeScript {
    "main" <== ["helloworld.exe"]
    "helloworld.exe" ..> csc { targetfwk "net-4.6.2"; src !!"helloworld.cs" }
}
```

The package `Xake.Dotnet` holds two assemblies:

| Assembly | Namespace | What |
|---|---|---|
| `Xake.Dotnet` | `Xake.Dotnet` | `csc`, `fsc`, `msbuild`, `resgen`, `resourceset`, project descriptions (`xake.project.json`), toolchain discovery |
| `Xake.Hermetic.Dotnet` | `Xake.Hermetic.Dotnet` | lock files, restore, SBOM, verification, deterministic packing, signing (API may change) |

Up to Xake 3.x the tasks shipped inside the `Xake` package and the hermetic part was the package
`Xake.Hermetic.Dotnet` (ended at 0.10.0). A script that referenced `Xake.Hermetic.Dotnet`
replaces that `#r` line with `#r "nuget: Xake.Dotnet"`; `open` lines are unchanged.

Package readme with the task overview: [src/dotnet/readme.md](src/dotnet/readme.md). Docs:
[docs/tasks.md](docs/tasks.md) (the task reference: `csc`, `fsc`, `msbuild`, `resgen`, script variables),
[docs/dotnet-build.md](docs/dotnet-build.md) (toolchain discovery),
[docs/description.md](docs/description.md) (project descriptions),
[docs/hermetic/README.md](docs/hermetic/README.md) (reproducible builds). The engine, its
docs and the wiki live in [OlegZee/Xake](https://github.com/OlegZee/Xake).

## Building

.NET SDK 8.0 or newer (`global.json`); the engine comes from nuget.org.

```bash
dotnet fsi build.fsx -- -- build test pack   # both assemblies, both test projects, the package
dotnet fsi build.fsc.fsx -- -- build check   # the same libraries compiled by fsc from the committed locks
```

See [docs/devprocess.md](docs/devprocess.md) for tests, versions and releases, and
[docs/release-notes.md](docs/release-notes.md) for what changed.

## History

The 1.x tasks of 2018 (package `Xake.Dotnet` 1.1.x) are on `master` before 4.0 and under the
tags `v1.1.0`, `v1.1.4`. The current code came from the Xake repository with its history
(`tools/import-from-xake.py`); `(OlegZee/Xake#N)` in those commit subjects names the Xake pull
request.
