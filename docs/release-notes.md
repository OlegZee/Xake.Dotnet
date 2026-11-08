# Release notes

`Xake.Dotnet` (both assemblies, `Xake.Dotnet` and `Xake.Hermetic.Dotnet`). The history before
4.0, when the .NET tasks shipped inside the `Xake` package (up to 3.15) and the hermetic part
was the package `Xake.Hermetic.Dotnet` (up to 0.10.0), is in
[Xake's release notes](https://github.com/OlegZee/Xake/blob/master/docs/release-notes.md) and,
for the hermetic part, in [docs/hermetic/README.md](hermetic/README.md).

## 4.3.0 (unreleased)

### Satellite assemblies (S71 reopened)

A C# project's culture-specific resources (`Strings.ja.resx`) are no longer dropped by the
import: the lock records them, and a script builds satellite assemblies on request, from the lock
or without one. [hermetic/lock.md](hermetic/lock.md#satellite-assemblies), [tasks.md](tasks.md#satellite).

- **`satellite { }`** (`Xake.Dotnet`): a satellite from a built main assembly (`template`) and
  resource sets, no project or lock: the version, attributes, public key and framework read from
  the template's metadata, the culture and output from the rule's target.
- **Record**: `Recorded.Satellites` with the resources per culture, written only for a project
  with culture resources (every other lock is unchanged). An F# project's are reported and left
  out; under invariant globalization a culture-shaped resource fails the import. A re-import adds
  the key to the locks of such projects.
- **Build**: `Locks.satellite settings lockPath fwk inputs`, the rule of
  `<dir>/<culture>/<Name>.resources.dll`, and `Locks.satellitesOf` to list the lock's; the
  assembly's rule builds none. `SatelliteInputs` adds translations kept outside the project
  (`resourceset`, read with `getFiles`), attributes, and whether a resource without its neutral
  one warns or fails.
- One recipe for every SDK, SDK 10's: no SDK targets read, no installed SDK needed. Byte-identical
  to `dotnet build` (SDK 8 and 10) on Linux and macOS; on Windows too, but the FileVersion of
  hand-written attributes without one. The satellites load on .NET Framework 4.8 and .NET.
- `.restext` resources are compiled (by `csc { }` too), as msbuild's `GenerateResource` does.
- Checks: the culture's spelling, the public key token against the main assembly's, the neutral
  resource names.
- Not yet: a referenced project's satellites in a run folder and their `deps.json` lines; an F#
  project's satellites from its lock.

## 4.2.0 (2026-11-07)

### The version as a token in a recorded lock

A CI build stamps its own version (`3.1.0-ci.512`) into the assemblies, an application's
`deps.json` and the packages without an import: the lock stays the same for every build. How-to:
[hermetic/versioning.md](hermetic/versioning.md).

- **Record** with `Import.RecordOptions.VersionToken = true`: msbuild runs with an
  internal marker suffix, and the import writes `$(Version)` where the project's version went
  (`InformationalVersion`, the `Version` property), the prefix as `Properties.VersionPrefix`, and
  `$(Version:<file>)` for each project's version in a recorded `deps.json`. `AssemblyVersion` and
  `FileVersion` stay literals (`<prefix>.0`), so BAML, `Recorded.Markup` and binding redirects
  do not change. Imports with any suffix write the same lock; the first one changes each entry
  once. Without the option nothing changes. A project that sets `<Version>` itself stays a
  literal (an Info line says so).
- **Build** with `Locks.Settings.VersionSuffixes` (a project mask to a suffix recipe, the first
  matching wins, masks may start with a root): the version is the prefix and `-<suffix>`, or the
  prefix alone for an empty suffix. `Versions` still gives a whole version and wins. A tokenized
  lock with no matching mask fails the build, and so does a suffix that is not a SemVer 2.0
  pre-release label (`+`, `_`, a leading `-`). A new suffix recompiles the entries with the token
  and what references them; the same suffix compiles nothing.
- **Packages**: `Locks.versionOf settings lockPath fwk` is the entry's version as the build
  stamps it, for `Pack.tokensOf`. `Pack` sets no versions of dependencies between packages: the
  nuspec's tokens do, from the dependency's `Locks.versionOf`.
- The SBOM's root component takes a tokenized version from the built assembly.
- **API**: `Locks.Settings` and `Import.RecordOptions` have a new field each: a script that
  builds them as a full record literal adds it; `{ Locks.Settings.Default with ... }` and
  `{ Import.RecordOptions.Default with ... }` need nothing.

## 4.1.0 (2026-11-06)

- **WPF projects are recorded** (S69; `Import.record` refused them). A project compiling XAML
  (`UseWPF` with `Page`/`ApplicationDefinition` items) gets runs of its own in which the markup
  compiler runs against the referenced projects' outputs, built from their locks (`OutOf`) and
  placed in the run's `OutDir`, and with the compiler running (the second markup pass compiles a
  temporary assembly). Its generated sources go into the lock, its `g.resources` beside it, its
  XAML and `Resource` files into `Evaluation.Imports`. Record the WPF projects after building
  their references ([workflows](hermetic/workflows.md#wpf-projects)).
- **The recorded BAML's references are checked** (`Recorded.Markup`, a new optional key): the
  referenced projects' assemblies the BAML names, with the version the markup compiler loaded.
  A build whose referenced output has another identity fails with "run the import again"
  instead of producing XAML that fails to load at run time. Locks without XAML do not change.
- **A project reference may name a project an earlier import recorded** (its lock exists), not
  only one of the same `Import.record` call.
- `Compilation.Resources` holds the `.resources` compiled from a `.resx` only; another
  `EmbeddedResource` with an `OutputResource` (the WPF `g.resources`) is copied beside the lock.
- The import's restore folder (`xake-record/<key>/`) is appended once: a project that starts
  from it (WPF's temporary assembly project) found no assets file.

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
