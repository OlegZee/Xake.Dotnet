The .NET tasks of [Xake](https://github.com/OlegZee/Xake), a make utility for .NET in F#
inspired by [shake](https://github.com/ndmitchell/shake): `csc`, `fsc`, `msbuild`, `resgen`,
`resourceset` and project descriptions (`Xake.Dotnet`), and reproducible builds (lock files,
restore, SBOM, verification, packing and signing; `Xake.Hermetic.Dotnet`). Both assemblies
ship in this one package, which depends on the engine package `Xake`, so one reference is
enough:

```fsharp
#r "nuget: Xake.Dotnet"   // brings Xake too; pin it (`, X.Y.Z`) for a reproducible script
```

Until Xake 3.x the .NET tasks shipped inside the `Xake` package and the hermetic part was the
package `Xake.Hermetic.Dotnet` (ended at 0.10.0). From 4.0 a script adds the line above; a
script that referenced `Xake.Hermetic.Dotnet` replaces that line with it. `open Xake.Dotnet`
and `open Xake.Hermetic.Dotnet` are unchanged.

See [Xake documentation](https://github.com/OlegZee/Xake/wiki/Introduction) for the engine and
[the task reference](https://github.com/OlegZee/Xake.Dotnet/blob/dev/docs/tasks.md) for the full set of options.

## Prerequisites

The .NET SDK 8.0+ (see `global.json`). Nothing else: the compilers come from the SDK and the
.NET Framework reference assemblies from the
`Microsoft.NETFramework.ReferenceAssemblies.*` packages, restored on first use -- so
full-framework binaries can be built on any OS without a Framework installation.

## Csc task

The simple script looks like:

```fsharp
#r "nuget: Xake.Dotnet"

open Xake
open Xake.Dotnet

do xakeScript {
  rules [
    "main" <== ["helloworld.exe"]

    "helloworld.exe" ..> csc {src !!"helloworld.cs"}
  ]
}
```

This script compiles helloworld assembly from helloworld.cs file. See
[samples/fullframework.fsx](../../samples/fullframework.fsx) for targeting a specific framework,
and [docs/dotnet-build.md](../../docs/dotnet-build.md) for how the toolchain is discovered and
how to switch between the SDK, a Framework installation and Mono.

`csc` also takes `toolset "<version>"` (a pinned `Microsoft.Net.Compilers.Toolset`, see
[samples/toolset.fsx](../../samples/toolset.fsx)), `noserver`/`keepalive` (compiler server; the
script variable `CSC_SERVER` sets it for the whole script) and `resolve` (returns the resolved
`Csc` for `Csc.run` instead of compiling). From record syntax use `Csc.compile settings`. All of
it is in [docs/tasks.md](https://github.com/OlegZee/Xake.Dotnet/blob/dev/docs/tasks.md).

## Reproducible builds (`Xake.Hermetic.Dotnet`)

> **API may change.** The hermetic part is still settling: until this note is removed, an
> incompatible change in it is a minor version of this package, with a release-notes line.

`open Xake.Hermetic.Dotnet` adds what a build needs to be repeatable and auditable: a lock file
that records exactly what the compiler is handed, with the SHA-256 of every reference, analyzer
and the compiler itself; a restore of the packages that lock names; an SBOM generated from the
lock; and the release steps after the compile. C# and F# alike, from `csc {}`/`fsc {}` or from
an imported `.csproj`/`.fsproj`. With `HERMETIC=on` a lock that names anything outside the
checkout and the build's package folder, other than an exactly pinned .NET SDK, is neither
written nor replayed.

Compile gated by a lock (recorded on the first build, enforced afterwards):

```fsharp
"out/app.dll" ..> csc { targetfwk "net472"; src !!"src/*.cs"; lock "locks/app.json" }
```

An F# library, gated the same way (the compiler is the SDK's `fsc.dll`: pin the SDK in
`global.json`):

```fsharp
"out/lib.dll" ..> fsc { targetfwk "net8.0"; src !!"src/*.fs"; lock "locks/lib.json" }
```

Restore the packages a lock names, checked against their recorded sha512:

```fsharp
let! doc = Lock.load "locks/app.json"
do! Lock.restore Restore.Options.Default doc
```

See [docs/hermetic/README.md](../../docs/hermetic/README.md) for the lock format,
`Project.import` (locks from existing project files), SBOMs, verification, packing and signing,
and [src/hermetic/readme.md](../hermetic/readme.md) for more examples.
