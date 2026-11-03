# Release notes

`Xake.Dotnet` (both assemblies, `Xake.Dotnet` and `Xake.Hermetic.Dotnet`). The history before
4.0, when the .NET tasks shipped inside the `Xake` package (up to 3.15) and the hermetic part
was the package `Xake.Hermetic.Dotnet` (up to 0.10.0), is in
[Xake's release notes](https://github.com/OlegZee/Xake/blob/dev/docs/release-notes.md).

## 4.0.0 (unreleased)

- **`Xake.Dotnet` is a package of its own**, built in this repository on the published `Xake`
  4.x package (range `[4.0.0, 5.0)`). It carries both assemblies: the .NET tasks (`csc`, `fsc`,
  `msbuild`, `resgen`, project descriptions) and the hermetic part (locks, restore, SBOM,
  verification, packing, signing). The `Xake` package carries the engine only.
- **Script change**: add `#r "nuget: Xake.Dotnet"` (it brings `Xake`); a script that had
  `#r "nuget: Xake.Hermetic.Dotnet"` replaces that line with it. `open Xake.Dotnet` and
  `open Xake.Hermetic.Dotnet` are unchanged.
- **`Xake.Hermetic.Dotnet` ends at 0.10.0**: that package keeps working on Xake 3.x and gets no
  updates; it cannot be mixed with `Xake.Dotnet` 4.x in one script.
- The hermetic part is marked "API may change": an incompatible change in it is a minor
  version of `Xake.Dotnet`, with a line here.
