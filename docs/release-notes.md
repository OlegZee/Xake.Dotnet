# Release notes

`Xake.Dotnet` (both assemblies, `Xake.Dotnet` and `Xake.Hermetic.Dotnet`). The history before
4.0, when the .NET tasks shipped inside the `Xake` package (up to 3.15) and the hermetic part
was the package `Xake.Hermetic.Dotnet` (up to 0.10.0), is in
[Xake's release notes](https://github.com/OlegZee/Xake/blob/master/docs/release-notes.md) and,
for the hermetic part, in [docs/hermetic/README.md](hermetic/README.md).

## Unreleased

### The import records the repository's spelling of each file

A project file that names a file in another case than the repository has it (`Src\A.FS` for
`src/A.fs`) imported on macOS or Windows gave descriptions and locks that do not build on Linux.
The imports (`Import.record`, `Import.descriptions`/`project`, `Project.import`) now write every
repository file as the git index spells it (`git ls-files`, once per import; without git nothing
changes), log each difference at Info with both spellings (`Imported.Casing`), and keep
msbuild's resource names. A `.resx`'s `ResXFileRef` files are recorded in the lock
(`ResxFiles`, optional; the lock format stays 2) and the resx compile reads them from there; a
miscased doc comment `<include file>` is reported. See
[description.md](hermetic/description.md#the-repositorys-spelling-of-a-file).

- **Import a miscased project again, once, on macOS or Windows.** Its description and locks
  then change (paths, and a resource `name` where the repository's spelling would derive
  another); a correctly spelled project gets the same files as before, except a lock whose
  `.resx` names files, which gains `ResxFiles`. A lock recorded before keeps failing on Linux as
  stale: the build looks nothing up ignoring case.
- API: `Import.Imported` has the field `Casing`; `Csc`, `Fsc` and the runner's plan have
  `ResxFiles` (a record expression of them needs `ResxFiles = []`).

## 4.0.0 (2026-11-03)

The first release from this repository, on `Xake` 4.0.0. The engine's own changes (patterns,
variables, typed targets) are in
[Xake's 4.0.0 notes](https://github.com/OlegZee/Xake/blob/master/docs/release-notes.md); below
is what a script that uses the .NET tasks or the hermetic library has to know.

### What the package is

- **`Xake.Dotnet` is a package of its own**, built in this repository on the published `Xake`
  package and depending on `Xake [4.0.0, 5.0)`. It carries two assemblies in
  `lib/net462` and `lib/netstandard2.0`:
  - `Xake.Dotnet` (`open Xake.Dotnet`): `csc`, `fsc`, `msbuild`, `resgen`, `resourceset`,
    project descriptions (`xake.project.json`), toolchain discovery;
  - `Xake.Hermetic.Dotnet` (`open Xake.Hermetic.Dotnet`): lock files, restore, SBOM,
    verification, deterministic packing, signing.
- The `Xake` package carries the engine only (from `4.0.0-preview.2`).
- **The hermetic part is marked "API may change"**: until that mark is removed, an incompatible
  change in it is a minor version of `Xake.Dotnet`, with a line here.

### Migration

| You had | Change in the script |
|---|---|
| `Xake` 3.x and `open Xake.Dotnet` | add `#r "nuget: Xake.Dotnet, 4.0.0"` (it brings `Xake`); `open Xake.Dotnet` unchanged |
| `Xake.Hermetic.Dotnet` 0.10.0 | replace `#r "nuget: Xake.Hermetic.Dotnet, 0.10.0"` with `#r "nuget: Xake.Dotnet, 4.0.0"`; `open Xake.Hermetic.Dotnet` unchanged |

```fsharp
// 3.x                                          // 4.0
#r "nuget: Xake, 3.15.0"                        #r "nuget: Xake.Dotnet, 4.0.0"
#r "nuget: Xake.Hermetic.Dotnet, 0.10.0"        open Xake
open Xake                                       open Xake.Dotnet
open Xake.Dotnet                                open Xake.Hermetic.Dotnet
open Xake.Hermetic.Dotnet
```

- **`Xake.Hermetic.Dotnet` ends at 0.10.0**: that package keeps working on Xake 3.x and gets no
  updates; it cannot be mixed with `Xake.Dotnet` 4.x in one script.
- The engine's migration checklist (patterns, variables, removed APIs) applies as well:
  [Xake 4.0.0, Migration checklist](https://github.com/OlegZee/Xake/blob/master/docs/release-notes.md#migration-checklist).

### Breaking: libraries read only the variable set

- **Both assemblies read their build variables only from the variable set**, never from the
  environment. `NUGET_PACKAGES`, `XAKE_CSC_SERVER`, `XAKE_CSC_JOBS`, `XAKE_FSC_JOBS` (Xake.Dotnet)
  and `CI`, `LOCK_REFRESH`, `SOURCE_DATE_EPOCH` (Xake.Hermetic.Dotnet) set only in the environment
  are ignored. A CI that relied on one declares it, e.g.
  `var "NUGET_PACKAGES" [Env "NUGET_PACKAGES"]`, `var "CSC_JOBS" [Env "XAKE_CSC_JOBS"]`,
  `var "CI" [Env "CI"]`, or passes `-d NAME=value`.
- **The package folder defaults to `<project root>/.packages`** (was `NUGET_PACKAGES`, else
  `~/.nuget/packages`). `PackageRootSource.EnvironmentVariable`/`UserProfile` are
  `[<Obsolete>]`; `HERMETIC=on` accepts the build's own folder without a variable. A child
  `dotnet restore` (and the msbuild of `Import.record`/`Project.import`) gets the folder as
  `NUGET_PACKAGES`.
- **Strict switches.** `NUGET_FETCH`, `XAKE_CSC_SERVER` (`on`/`off`, `true`/`false`, `yes`/`no`,
  `1`/`0`) and `CSC_SERVER` (`on`, `off`, keepalive seconds) fail the reading target on any other
  value; an empty value is the default. `CI` and `LOCK_STALE` keep their strict values; an empty
  `CI` is off, an empty `LOCK_STALE` is `fail`.
- `RunOptions.Default.Server` is `Shared None`; `CompilerServer.fromEnvironment` is
  `[<Obsolete>]` and returns `Shared None`. A recipe resolves the server from `CSC_SERVER`, then
  `XAKE_CSC_SERVER` (`CompilerServer.resolve`).
- `Pack.Options.Default` and `Sbom.PackageScopeOptions.Default` are the 1980 epoch always;
  `Pack.Options.current ()` and `Sbom.PackageScopeOptions.current ()` take `SOURCE_DATE_EPOCH`.
- **Optional schemas** list the variables in `--help` and the banner: `DotNetVars.Default`
  (`NUGET_PACKAGES`, `NUGET_FETCH`, `HERMETIC`, `CSC_SERVER`, `XAKE_CSC_SERVER`, `CSC_JOBS`,
  `FSC_JOBS`) and `HermeticVars.Default` (`CI`, `LOCK_REFRESH`, `LOCK_STALE`,
  `SOURCE_DATE_EPOCH`), e.g.
  `varschema { HermeticVars.Default with CI = Var.string(envVar = "CI") |> withDefault "off" }`.
  Without `envVar` a field reads `XAKE__<NAME>` (Xake 4.0).

### Breaking: Xake 4.0 patterns in masks and locks

Every mask the tasks take (`src`, `resourceset`, the masks of a description or a lock) follows
the Xake 4.0 pattern grammar: `( ) [ ] { } |` are reserved, `{a,b}` is an alternation, `[abc]` a
character class, `\` escapes, `dir/**` is every file below `dir`. See
[Xake 4.0.0, Patterns](https://github.com/OlegZee/Xake/blob/master/docs/release-notes.md#patterns).

- **The import escapes reserved characters.** The msbuild import (`Import.descriptions`,
  `Project.import`) escapes `( ) [ ] { } |` in the masks and project paths it
  records (`Foo (x86)` is written `Foo \(x86\)`), so such folders keep importing; msbuild's `*`,
  `?`, `**` stay wildcards and `dir/**` is still written `dir/**/*`. Descriptions and locks
  change only for paths holding those characters; one written by Xake 3.x or
  `Xake.Hermetic.Dotnet` 0.10 with such a path needs a re-import (`import`, then
  `update-locks`).
- A hand-written mask (`src !!"..."`, a description's `sources`) that holds a literal `(`, `)`
  or `|` is an `XPnnn` error, and `[...]`, `{a,b}` change meaning; escape them with `\`, or use
  `PatternAst.escapePath` for a real path.
