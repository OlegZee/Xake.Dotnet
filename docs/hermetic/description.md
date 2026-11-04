# Project description (`xake.project.json`)

A project description says what a project is: its name, language, project file, target
frameworks, sources, references, packages, options and package metadata. It is **data only**,
changes rarely, and is committed. An explicit import (or a person) writes it; the build reads it.
There are no placeholders: a variant that msbuild evaluates differently (a brand, for example)
gets its own description. Configurations, the output layout and versions belong to the build
script, not to the description.

The format belongs to the `Xake.Dotnet` assembly: module `Xake.Dotnet.Description`
(`src/dotnet/Description.fs`), schema
[../schemas/xake.project.schema.json](../schemas/xake.project.schema.json), every field, the
fixed text format and the API on [../description.md](../description.md). This page is what
Xake.Hermetic.Dotnet adds: the import that writes descriptions, and the composition of a
description with packages, a configuration and a lock.

## The composition this package adds: `config`, `packages`, `lock`

The core's `csc { project "path" }` / `fsc { project "path" }` compose a description by
themselves (sources, resources, defines, options, attributes, framework assemblies, shared
frameworks) and reject one with `packages`. This package adds operations after `project`:

```fsharp
"out/Release/netstandard2.0/Xake.dll" ..> recipe {
    let! d = Description.load "src/core/xake.project.json"
    let! pkgs = Described.restore d "netstandard2.0"
    do! fsc { targetfwk "netstandard2.0"; project "src/core/xake.project.json"
              config "Release"; packages pkgs; lock "locks/Xake/Release.json" }
}
```

| Operation | What it adds |
|---|---|
| `config "Debug"` / `config "Release"` | the SDK's preset (`Described.preset`): defines `TRACE;DEBUG` or `TRACE;RELEASE`, debug/optimize switches, F# tail calls |
| `packages p` | the compile-time assemblies of the packages `Described.restore` (or `restoreWith`) resolved, and their nuspec `frameworkAssemblies` for a .NET Framework target |
| `lock "path"` (then `nofetch`) | gates the compilation by the lock ([lock.md](lock.md)); a C# lock keeps the description's masks |
| `resolve` | the composed `Csc`/`Fsc` instead of compiling |

Any of them makes the compilation this package's (`Described.fsc`/`Described.csc`): the plan is
built from the core's `ProjectPlan` pieces (`pickFramework`, `itemsFor`, `frameworkDefines`,
`manifestName`, `sdkNoWarn`, `implicitFrameworkReferences`, `DotNetFwk.sharedFrameworkReferences`)
plus what only this package does: the restored packages, `--pathmap:<root>=/_/`, C# sources
kept as masks (expanded when compiled, so an added `.cs` changes no lock), generated files
under `obj/xake/<assembly>/<framework>[/<config>]`. As in 0.3, the block's `define`, `src` and
`args` add to the description's (the core's composition replaces with them), and a `projectRefs`
entry missing from `ref` is a warning. They come after `project` (`config "Release"` before it
does not type-check).

## Import

`Import.descriptions projects descriptionOf globals` (module `Import`, `src/hermetic/Import.fs`)
writes the description of each project file from msbuild's evaluation under `globals`, e.g.
`["Configuration", "Release"; "Brand", "Acme"]`; `descriptionOf` maps a project file to its
description file (`Description.beside`, or a brand folder). msbuild evaluates the project once
without and once per target framework, through the same runner as the lock import; no targets
run, nothing is restored, and a file is rewritten only when its content changes (an existing
`$schema` is kept). How the fields are taken:

- `name`: the project file name; `assemblyName`/`rootNamespace` only when they differ from it.
- `sources`: F#, the `Compile` items in order. C#, Xake masks, not files: `**/*.cs` and
  msbuild's default excludes (`!bin/**/*`, `!obj/**/*`, `!**/.*/**/*`), then the `Compile`
  `Include`/`Remove` attributes of the repository files; `!` marks an exclude, applied in order,
  and msbuild's `dir/**` is written `dir/**/*`. The masks are expanded and compared with
  msbuild's own list; a difference is reported.
- `defines`, `references`, `frameworkReferences`, `packages`: what every framework has; the rest
  under `byFramework`. `frameworkReferences` are msbuild's `FrameworkReference` items, the SDK's
  implicit ones included (the Web SDK's `Microsoft.AspNetCore.App`), without
  `Microsoft.NETCore.App`; the WindowsDesktop profiles are written `Microsoft.WindowsDesktop.App`.
  `TRACE` and the configuration's define, the SDK's implicit packages (`NETStandard.Library`,
  `System.ValueTuple`) and references are left out. Package versions are msbuild's
  (`Version`, `VersionOverride` or central `PackageVersion`); `PrivateAssets=all` is `private`.
  Item metadata is read whatever its case (`version="8.0.5"`, `privateassets`, `logicalname`).
- `resources`: the `EmbeddedResource` items, plain files and `.resx` alike, `LogicalName` as
  `name`. Without one, `name` is what msbuild names it when that is not the default of the file
  path: a `Link` gives the default name of the link path (`Link="Shared\x.txt"` ->
  `<ns>.Shared.x.txt`); `ManifestResourceName` on a `.resx` gives `<that>.resources`, on another
  file the file name (msbuild then sets no `LogicalName`, so csc names it after the file). A culture-specific one (`Strings.de.resx`: a satellite assembly) is left out and
  reported; one msbuild names after a class (`DependentUpon`, or a `.resx` with a `.cs` of the
  same name beside it) is kept with the default name and reported (set `name` by hand).
- `options`: `GenerateDocumentationFile`, `TreatWarningsAsErrors`, `Nullable`, `LangVersion`
  (only when a repository file sets it), the key file when `SignAssembly`, F# `OtherFlags`,
  `NoWarn` without the SDK's defaults (C# `1701;1702`). The lists `WarningsAsErrors` and
  `WarningsNotAsErrors` have no field: reported in `Dropped` when a repository file sets them,
  not seen at all when the SDK or an import outside the repository does.
- `assemblyAttributes`: the `AssemblyAttribute` items other than `InternalsVisibleTo` (those are
  `internalsVisibleTo`), arguments as msbuild's `WriteCodeFragment` writes them: `_Parameter1`,
  `_Parameter2`... positional, other metadata named (`Name = value`); `_ParameterN_IsLiteral`
  verbatim, `_ParameterN_TypeName` the type; else the types of the first constructor of the
  attribute type (when the importing process knows it, e.g. `System.CLSCompliantAttribute`:
  `true` stays `true`) that takes every value, strings otherwise. A string with `"` or `\` is
  written as a verbatim literal (`@"a""b"`).
- `package`, when `IsPackable`: `PackageId`, `Description` and `Authors` (when a repository
  file sets them), `Copyright`, `PackageTags`, `PackageLicenseExpression`, `PackageProjectUrl`,
  `RepositoryType`/`RepositoryUrl`, `PackageIcon`/`PackageReadmeFile` as the packed file's
  path; `bundle` is every `PrivateAssets=all` project reference.

Each result (`Import.Imported`) names the project and description files, whether it was
written, `Inputs` (the msbuild files read: the project, `Directory.Build.props`/`.targets` and
other repository imports; not the SDK's, package or `obj/` files), `Dropped` (what msbuild
knows that the description has no field for: other properties and item types of the repository
files, targets, attribute arguments it cannot write, the SDK's implicit packages) and `Casing`
(the files the project spells otherwise than the repository, below).

### The repository's spelling of a file

A project file may name a file in another case than the repository has it (`Src\A.FS` for
`src/A.fs`): msbuild accepts that where the file system ignores case (macOS, Windows) and the
import would write that spelling, which a build on Linux does not find. Every import
(`Import.descriptions`, `Import.record`, `Project.import`) therefore writes the **repository's
spelling** of each file, taken from the git index: `git ls-files -z --full-name`, run once per
import in the project root (`Casing.read`; the index is never parsed by Xake). A path outside
the project root, or one the index does not have (an untracked or generated file), is written as
msbuild spells it. Without git, or outside a repository, nothing changes and one line at Info
says so.

- What changes is only the **path** of a file: the project files the script names (msbuild then
  evaluates them so spelled), the description's `project`, `sources` (the F# list; for C# the
  literal part of each mask, wildcards kept), `resources[].file`, `keyFile`, `projectRefs` (a
  reference's name is the repository's name of the project file), the packed icon and readme,
  and in a lock the compiler's command line, `Resources`, `Evaluation.Imports`, an application's
  files and the referenced projects.
- A **resource keeps the name msbuild gives it** from the project's spelling: where the
  repository's spelling would derive another default name, the description writes msbuild's as
  `name` (`Resources\Strings.RESX` -> file `resources/Strings.resx`, name
  `App.Resources.Strings.resources`); a lock's `/resource:<file>,<name>` keeps its name, and a
  `/resource:<file>` without one (named after its file name) gets the project's file name as its
  name.
- **No file's content is rewritten.** A `.resx` naming a file (`ResXFileRef`) in another case is
  left as it is; `Import.record` records the repository's file for that spelling in the lock
  (`ResxFiles`, [lock.md](lock.md#recorded-locks-importrecord)), and the resx compile reads it
  from there. A C# doc comment's `<include file='...'>` in another case (csc reads it relative to
  the source) is reported; the fix is the source's.
- Two entries of the index that differ only by case (`Docs/a.xml`, `docs/A.xml`): the one spelled
  exactly as the project spells it is taken, else the import fails naming both.

Every difference is a line at Info in the import log, once per import, with both spellings, and
is in `Imported.Casing`:

```
  casing (App.csproj): 'app/Code/Program.CS' is 'app/code/Program.cs' in the repository (git index): recorded so
  casing (App.csproj): 'app/Images/Copy.TXT' (ResXFileRef '..\Images\Copy.TXT' in 'app/resources/Strings.resx') is 'app/images/copy.txt' in the repository (git index): a lock records that file for it, the .resx is not changed
  casing (App.csproj): 'app/code/Docs/Common.XML' (<include file='Docs\Common.XML'> in 'app/code/Program.cs') is 'app/code/docs/common.xml' in the repository (git index): csc reads it as the source spells it; fix the spelling in the source
```

The build and the replay of a lock read exactly the paths it names, on every operating system:
there is no case-insensitive lookup. A lock or description recorded before this (by
`Xake.Dotnet` 4.0.0) with a miscased path keeps failing on Linux (a miscased `Imports` path
makes the lock stale); import it again once, on macOS or Windows.

### What an import needs, and how it fails

**Requirement: MSBuild 17.10 or later, that is .NET SDK 8.0.300 or later**, for every import
(`Import.descriptions`, `Import.record`, `Project.import`): each passes msbuild's
`-getResultOutputFile`, which came with MSBuild 17.10 (`-getProperty`/`-getItem` are 17.8, but
they are not enough). Before its first restore an import runs `dotnet msbuild -version` once in
the project root (`ExecOptions.ProjectRoot`, where `global.json` selects the SDK) and refuses an
older MSBuild:

```
the import needs MSBuild 17.10 or later (.NET SDK 8.0.300+, or any 9.x/10.x SDK); found 17.8.3 (SDK 8.0.121): raise the pin in global.json (<file>)
the import needs MSBuild 17.10 or later (.NET SDK 8.0.300+, or any 9.x/10.x SDK); found 17.8.3 (SDK 8.0.121): install SDK 8.0.300 or later
```

The first when a `global.json` at or above the project root pins the SDK, the second otherwise.
A version the check cannot read skips it (a line at Info says so). Building from a lock needs no
msbuild and has no such floor.

Every msbuild run of an import runs in the project root, so `global.json` is looked up where the
SDK probe looks, whatever the process's current directory. msbuild's diagnostics go to the log
by their shape (`<project> : error NU1008: ...`, `<file>(1,2): error CS0103: ...`,
`MSBUILD : error MSB1001: ...`, `error NU1301: ...`): errors at Error, warnings at Info, each
distinct warning once per build run; other lines stay at Verbose (the core's
`Tool.diagnosticLevel` and `Tool.DiagnosticLog`, Xake 3.13). A failed run fails the import
naming its first five distinct errors, each with its project (the `[entry]` suffix dropped):

```
Exiting due to FailOnError set on '<root>/obj/xake/import/<id>/import.sln'
  /repo/App/App.csproj : error NU1008: The following PackageReference items cannot define a value for Version: ...
  +2 more, see the log
```

The restore of a description's packages (`Described.restore`) reports its failures the same way.

## Import that records msbuild's compile inputs: `Import.record`

`Import.record options projects` writes, for each project file, its description (as
`Import.descriptions` does) **and** one lock per configuration (`options.LockOf name cfg`), with
one entry per target framework. The lock entry is msbuild's own answer, not a Xake
derivation: see [lock.md, "Recorded locks"](lock.md#recorded-locks-importrecord). For the
description, `record` differs from `descriptions` in two points:

- The resource names and cultures come from msbuild's targets: `ManifestResourceName` and
  `LogicalName` (a `.resx` is `<ManifestResourceName>.resources`), `WithCulture` for a
  satellite resource. `name` is written only when it is not the default name. No C# parsing and
  no culture table of the importing machine are used.
- `byFramework.<tfm>.resources` holds the resources only one framework has.

For a recorded project the build takes the source masks from the lock (`Recorded.Masks`), not
from the description: an edit of the description's `sources` has no effect on the build. Change
the project file and import again.

An application (an exe, a winexe, a .NET test project) has nothing more in its description:
the apphost, `runtimeconfig.json`, `deps.json`, the config file and the copy-local files are in
the lock's `App` section, recorded from msbuild
([lock.md, "Applications"](lock.md#applications-the-app-section)). The description schema has
no field for them.

Both imports read a `Compile Include` with a property (`$(BuildTmpPath)\VersionInfo.cs`, a
shared project's `$(MSBuildThisFileDirectory)X.cs`) as the evaluated path, relative to the
project folder, and write the path of a shared project's resource relative to the project
folder.

| `RecordOptions` field | Meaning |
|---|---|
| `Configurations` | one lock per configuration, e.g. `["Release"]` |
| `Properties` | the global properties msbuild runs with, e.g. `["Brand", "Globex"]` (not `Configuration`); written into the lock, tokenized |
| `Roots` | the script's extra roots (`Roots.make`) |
| `DescriptionOf` | project file -> description file (default `Description.beside`) |
| `LockOf` | description name -> configuration -> lock file |
| `OutOf` | configuration -> framework -> project name -> output folder: where a project reference is found |
| `PacksFrom` | where the targeting, apphost and runtime packs come from: `Import.NuGet` (default) or `Import.Installed`, see [lock.md](lock.md#recorded-locks-importrecord) "Packs" |

## API

| Function | Signature | What it does |
|---|---|---|
| `Description.*` | | the core's (`Xake.Dotnet.Description`): see [../description.md](../description.md#api) |
| `Import.descriptions` | `string list -> (string -> string) -> (string * string) list -> Recipe<ExecContext, Import.Imported list>` | import project files into descriptions (above) |
| `Import.project` | `string -> (string -> string) -> (string * string) list -> Recipe<ExecContext, Import.Imported>` | the same for one project |
| `Import.record` | `Import.RecordOptions -> string list -> Recipe<ExecContext, Import.Imported list>` | descriptions and recorded locks ([above](#import-that-records-msbuilds-compile-inputs-importrecord)); `Imported.Locks` names the locks written |

```fsharp
open Xake
open Xake.Dotnet
open Xake.Hermetic.Dotnet

let descriptions = !! "src/*/xake.project.json"

"pack-xake" => recipe {
    let! d = Description.find descriptions "Xake.Dotnet"   // depends on that one file only
    do! trace Info "%s targets %A" d.Name d.Frameworks
}
```
