# Versioning: a build's version from a recorded lock

A CI build stamps its own version (`3.1.0-ci.512`) into the assemblies, the `deps.json` and the
packages, while the lock stays the same for every build. The import records the version as a
token; the script supplies the suffix per build. This page is the how-to; the reference is in
[lock.md](lock.md#building-from-xake-generated-locks) ("A recorded version token").

```fsharp
#r "nuget: Xake.Dotnet, 4.2.0"   // brings Xake; VersionSuffixes is new in 4.2

open Xake
open Xake.Dotnet
open Xake.Hermetic.Dotnet
```

## The model

```
Version = VersionPrefix          -  suffix
          3.1.0                     ci.512
          recorded in the lock      per build, from the script (Locks.Settings.VersionSuffixes)
          (Directory.Build.props)   empty: the prefix alone, "3.1.0" (no trailing "-")
```

| Changes with the suffix | Stays `<prefix>.0` (a literal in the lock) |
|---|---|
| `AssemblyInformationalVersion` (the file's "Product version") | `AssemblyVersion` |
| the `Version` property (`Locks.versionOf`) | `AssemblyFileVersion` (the file's "File version") |
| an application's `deps.json`: its own and each referenced project's library entry | assembly references, binding redirects |
| the package version (the script passes `Locks.versionOf`) | WPF BAML and `Recorded.Markup` (they name `AssemblyVersion`) |

So a new build number changes no assembly identity: a referencing assembly, an `App.config`
redirect or a compiled XAML stays valid. A new **prefix** does change `AssemblyVersion`: it is an
edit of `Directory.Build.props`, an import input, so the lock is stale and the build fails with
*run the import again*. Raise the prefix, re-import, commit.

## Recording

Turn the option on in the import:

```fsharp
command "import" {
    let! (Filelist files) = getFiles (!! "src/*/*.csproj")
    let! _ = Import.record
                { Import.RecordOptions.Default with
                    LockOf = lockOf; OutOf = outOf
                    VersionSuffix = Some Import.versionMarker }
                [ for f in files -> f.FullName ]
    () }
```

msbuild runs with `VersionSuffix=xakeversionmarker`, so the SDK computes
`Version = <prefix>-xakeversionmarker` and puts it wherever `$(Version)` goes. The import turns
every such version into a token and records the prefix. The first import with the option changes
each entry once; later imports, with any suffix or none, write the same lock:

```diff
 "Evaluation": { "Properties": {
-    "Version": "3.1.0",
+    "Version": "$(Version)",
+    "VersionPrefix": "3.1.0",
 ...
 "Generated": { "$(ProjectRoot)/obj/xake/app/net8.0/Release/app.AssemblyInfo.cs":
-    "... AssemblyInformationalVersionAttribute(\"3.1.0+$(SourceRevisionId)\")] ..."
+    "... AssemblyInformationalVersionAttribute(\"$(Version)+$(SourceRevisionId)\")] ..."
```

and in the recorded `deps.json` (`<lock>.files/<fwk>/app/<app>.deps.json`), each project's
version by the file it is read from at build time:

```diff
-      "app/3.1.0": {
+      "app/$(Version:app.dll)": {
-          "core": "1.4.0"
+          "core": "$(Version:core.dll)"
```

`AssemblyVersionAttribute("3.1.0.0")` and `AssemblyFileVersionAttribute("3.1.0.0")` stay as they
were. The token also replaces the version in an option or a define that carried it.

- **A project that sets `<Version>` itself** never sees the marker. It is recorded with its literal,
  and the import says so at `Info`:
  `'fixed' (Release|net8.0): the project sets its version itself (9.9.9); it is recorded as a literal, the build's version suffix does not change it`.
  Change the project to `<VersionPrefix>` if it should take the suffix.
- **An environment variable `VERSION`** (a release workflow exports one) would become msbuild's
  `$(Version)`; the import empties it for its msbuild runs.
- **The marker** is `Import.versionMarker`. Another one must be letters and digits, starting with a
  letter. The script's own `VersionSuffix` in `RecordOptions.Properties` fails the import
  (*... and the script sets the property VersionSuffix too; drop one*).
- **A stray marker fails the import**, naming the file: a target that writes `$(VersionSuffix)` on
  its own (an `AssemblyMetadata` item, a custom generated file), or a version built some other
  way. The lock cannot say what that text should be at build time:
  `'odd' (Release|net8.0): the version marker xakeversionmarker is left where it is not the project's version <prefix>-xakeversionmarker (.../odd.AssemblyInfo.cs); a version made from $(VersionSuffix) alone cannot be a token`.
  Build the text from `$(Version)` instead.

## Supplying the suffix

`Locks.Settings.VersionSuffixes : (string * Recipe<ExecContext, string>) list` maps a project
mask to a recipe returning the suffix. The same `settings` go to `Locks.compile` (the assembly
rule) and `Locks.versionOf` (packages). A complete script:

```fsharp
let lockOf name cfg = $"locks/{name}/{cfg}.json"
let outOf cfg fwk name = $"out/{cfg}/{fwk}/{name}"

let vars = {| Suffix = Var.string(name = "SUFFIX") |> withDefault "dev" |}

let settings =
    { Locks.Settings.Default with
        VersionSuffixes = [ "**", recipe { let! s = vars.Suffix in return s } ] }

let compile = recipe {
    let! cfg = getRuleMatch "cfg"
    let! fwk = getRuleMatch "fwk"
    let! name = getRuleMatch "name"
    do! Locks.compile settings (lockOf name cfg) fwk }

do xakeScript {
    varschema vars
    rules [
        command "build" { do! Locks.needAll (!! "src/*/xake.project.json") "Release" lockOf outOf }
        target "out/(cfg:*)/(fwk:*)/(name:*)/*.dll" { do! compile }
        target "out/(cfg:*)/(fwk:*)/(name:*)/*.exe" { do! compile }
    ]
}
```

```bash
dotnet fsi build.fsx -- -- build                      # 3.1.0-dev
dotnet fsi build.fsx -- -- build -d SUFFIX=ci.512     # 3.1.0-ci.512
XAKE__SUFFIX=ci.512 dotnet fsi build.fsx -- -- build  # the same, from the environment
dotnet fsi build.fsx -- -- build -d SUFFIX=           # 3.1.0, a release
```

The variants below replace `vars` and `VersionSuffixes`; the rest of the script stays. The
variables are Xake's ([variables.md](https://github.com/OlegZee/Xake/blob/dev/docs/variables.md)).

### A constant; the release

```fsharp
VersionSuffixes = [ "**", recipe { return "preview" } ]   // 3.1.0-preview
VersionSuffixes = [ "**", recipe { return "" } ]          // 3.1.0: the prefix alone, no trailing "-"
```

### A variable: `withDefault` or `required`

```fsharp
let vars = {| Suffix = Var.string(name = "SUFFIX") |> withDefault "dev" |}   // -d SUFFIX=..., XAKE__SUFFIX
let vars = {| Suffix = Var.string(name = "SUFFIX") |> withDefault "" |}      // a forgotten SUFFIX: a release
let vars = {| Suffix = Var.string(name = "SUFFIX") |> required |}            // a forgotten SUFFIX: the run stops
```

- `withDefault ""`: a CI job that forgets the variable silently builds `3.1.0`, which looks like a
  release. Prefer a default that cannot be mistaken for one (`dev`, `local`).
- `required`: the run stops at launch, before any target (`Required variable 'SUFFIX' is not
  provided`). That is every run of the script, a local `import` or `clean` too.
- Optional, failing only where a version is needed (the assembly rules of tokenized locks, the
  pack rules):

  ```fsharp
  let vars = {| Suffix = Var.string(name = "SUFFIX") |}   // string option
  let suffix = recipe {
      let! s = vars.Suffix
      return s |> Option.defaultWith (fun () -> failwith "SUFFIX is not set: -d SUFFIX=<label>, or -d SUFFIX= for a release") }
  let settings = { Locks.Settings.Default with VersionSuffixes = [ "**", suffix ] }
  ```

### The CI's own variable

Read through a declaration, listed in `--help` and the banner:

```fsharp
let vars = {| Run = Var.string(name = "RUN", envVar = "GITHUB_RUN_NUMBER") |> withDefault "0" |}
let settings =
    { Locks.Settings.Default with
        VersionSuffixes = [ "**", recipe { let! n = vars.Run in return $"ci.{n}" } ] }
```

It reads `GITHUB_RUN_NUMBER` only (an explicit `envVar` replaces `XAKE__RUN`); `-d RUN=7` still
wins. GitLab: `CI_PIPELINE_IID`; Azure Pipelines: `BUILD_BUILDID`.

Or read the environment in the recipe with `getEnv`, recorded as a dependency too, but not
declared (not in `--help`; it reads the environment even under `IgnoreEnvironment`):

```fsharp
VersionSuffixes = [ "**", recipe {
    let! n = getEnv "GITHUB_RUN_NUMBER"
    return match n with Some n -> $"ci.{n}" | None -> "local" } ]
```

### A `var` rule and `getVar`

```fsharp
do xakeScript {
    var "SUFFIX" [ Cli "suffix"; Env "BUILD_SUFFIX"; Default "dev" ]   // -d suffix=..., else $BUILD_SUFFIX, else dev
    ...
}
let settings =
    { Locks.Settings.Default with
        VersionSuffixes = [ "**", recipe {
            let! s = getVar "SUFFIX"
            return s |> Option.defaultValue "dev" } ] }
```

An `Env` source that is present but empty counts as a value: `BUILD_SUFFIX=` gives a release.

### Composed: channel and build number

```fsharp
let vars = {|
    Channel = Var.string(name = "CHANNEL") |> withDefault "dev"     // dev, beta, rc, release
    Build   = Var.string(name = "BUILD", envVar = "GITHUB_RUN_NUMBER") |> withDefault "0"
|}
let suffix = recipe {
    let! channel = vars.Channel
    let! build = vars.Build
    return
        match channel with
        | "release" -> ""                        // 3.1.0
        | c -> $"{c}.{build}" }                  // 3.1.0-beta.512
let settings = { Locks.Settings.Default with VersionSuffixes = [ "**", suffix ] }
```

```bash
dotnet fsi build.fsx -- -- build -d CHANNEL=beta     # with GITHUB_RUN_NUMBER=512: 3.1.0-beta.512
dotnet fsi build.fsx -- -- build -d CHANNEL=release  # 3.1.0
```

### A suffix per project group

Two groups, each with its own prefix from its own `Directory.Build.props` (`app/**` at 3.1.0,
`vendor/core/**` at 1.4.0), each with its own suffix:

```fsharp
let vars = {|
    AppSuffix  = Var.string(name = "APP_SUFFIX")  |> withDefault "dev"
    CoreSuffix = Var.string(name = "CORE_SUFFIX") |> withDefault "dev"
|}
let settings =
    { Locks.Settings.Default with
        VersionSuffixes =
            [ "app/**", recipe { let! s = vars.AppSuffix in return s }
              "**",     recipe { let! s = vars.CoreSuffix in return s } ] }
```

```bash
dotnet fsi build.fsx -- -- build -d APP_SUFFIX=alpha.7 -d CORE_SUFFIX=beta.3
# app.dll 3.1.0-alpha.7, core.dll 1.4.0-beta.3; app.deps.json: "app/3.1.0-alpha.7", "core/1.4.0-beta.3"
```

The application's `deps.json` names each project with that project's own version: it is read
from the built `core.dll`, whatever group the application is in.

### A variant tag on one group

A distribution variant adds `-eu` to one group. Appending to an empty suffix gives `-eu`, which
the build refuses (a suffix must not start with `-`, see [A valid suffix](#a-valid-suffix)):

```fsharp
"brands/eu/**", recipe { let! s = vars.Suffix in return s + "-eu" }   // SUFFIX= fails the build
```

Handle the empty case:

```fsharp
[ "brands/eu/**", recipe {
      let! s = vars.Suffix
      return if s = "" then "eu" else s + "-eu" }      // 3.1.0-eu, 3.1.0-ci.512-eu
  "**", recipe { let! s = vars.Suffix in return s } ]
```

A tag in the suffix makes every version of that group a pre-release, the release build's too
(`3.1.0-eu`). If the variant's release must be a stable version, give it its own prefix (its own
`Directory.Build.props`) or its own whole version through `Versions`, below.

### A whole version for one group: `Versions`

`Locks.Settings.Versions : (string * RequiredVar<string>) list` gives the whole `$(Version)` from
a variable; the recorded prefix is not used. It is checked first: a project any `Versions` mask
matches takes that variable, whatever `VersionSuffixes` says.

```fsharp
let vars = {|
    ToolsVersion = Var.string(name = "TOOLS_VERSION") |> withDefault "0.0.0-dev"
    Suffix       = Var.string(name = "SUFFIX") |> withDefault "dev"
|}
let settings =
    { Locks.Settings.Default with
        Versions        = [ "tools/**", vars.ToolsVersion ]
        VersionSuffixes = [ "**", recipe { let! s = vars.Suffix in return s } ] }
```

```bash
dotnet fsi build.fsx -- -- build -d TOOLS_VERSION=2.0.0-rc.1 -d SUFFIX=ci.512
# tools/**: 2.0.0-rc.1; everything else: <its prefix>-ci.512
```

`AssemblyVersion` and `FileVersion` still come from the lock (`<recorded prefix>.0`): a
`TOOLS_VERSION` of another major than the recorded prefix gives an assembly whose informational
version and assembly version disagree.

## Which mask applies

- A project is matched as `<its folder>/xake.project.json`, relative to the project root
  (`xake.project.json` for a project at the root): `app/**` covers every project under `app/`.
  The folder is the project's (`Directory` of the lock's compilation), not where its lock lives.
- `Versions` first, then `VersionSuffixes`; within each list **the first matching mask wins, in
  list order**. There is no specificity: a broad mask listed first shadows every narrower one after
  it.

  ```fsharp
  [ "**", common; "brands/eu/**", eu ]   // wrong: eu is never used
  [ "brands/eu/**", eu; "**", common ]   // right: narrow first, ** last as the fallback
  ```

- A mask may start with a script root, for projects in another checkout: `"$(Sibling)/**", s`, with
  the root in `Settings.Roots` (`Roots = [ "$(Sibling)", "../sibling-repo" ]`).
- Only an entry that carries the token needs a mask. A project with its own `<Version>`, or a lock
  recorded without the option, builds with its literal and reads no variable.
- **No match is an error**, by design: no silent default, so a misconfigured CI cannot ship a
  release-looking `3.1.0`. The build fails with

  ```
  '<name>': the lock '<lock>' needs a version ($(Version)) and no mask of `Versions` or `VersionSuffixes` matches '<folder>/xake.project.json' (<masks>)
  ```

  To have a default, say so: `"**", recipe { return "dev" }`, last.
- A lock carrying `$(Version)` without `VersionPrefix` (one not recorded with the option: composed,
  hand-written) cannot take a suffix; a `VersionSuffixes` mask matching it fails with
  *... records no VersionPrefix for the suffix of `VersionSuffixes` ...; give '<path>' a mask of
  `Versions`*. List a `Versions` mask for it (they are checked first).

## What rebuilds

The suffix recipe runs inside each assembly's rule (`Locks.compile`), so every variable it reads
(`vars.X`, `getVar`, `getEnv`) is a dependency of that assembly. Only the recipe of the matching
mask runs.

- Same suffix as the last build: nothing is compiled.
- A new suffix for a group: that group's assemblies are compiled again (their bytes change), then
  what references them (a reference's output is an input), and an application's `deps.json` is
  written again with the new versions. Other groups stay.
- Moving an equal value between `-d`, `XAKE__<KEY>` and a default rebuilds nothing: a read records
  the value, not its source.
- A build number in the suffix therefore recompiles every tokenized assembly on every CI build;
  that cannot be avoided, the bytes differ. Keep local builds on a constant suffix.

What loses the dependency (an assembly keeps the old version, or groups get different ones):

| Don't | Do |
|---|---|
| `System.Environment.GetEnvironmentVariable "BUILD_NUMBER"` | `getEnv "BUILD_NUMBER"`, or a `Var.string(envVar = "BUILD_NUMBER")` field |
| a value computed at the top of the script and closed over (`recipe { return suffix }`) | read the variable inside the recipe |
| `DateTime.Now` (differs per assembly, recorded nowhere) | a variable the CI sets once per build (a run number) |
| `File.ReadAllText "version.txt"` | `do! need [ "version.txt" ]` before reading it |

The masks themselves are script code, not a dependency: editing `Versions` or `VersionSuffixes`
(a new mask, moving a group to `Versions`) rebuilds nothing while the variables read last time
keep their values. Clean the outputs after such an edit.

## Packages

`Locks.versionOf settings lockPath fwk : Recipe<ExecContext, string>` is the entry's `Version` as
the build stamps it: the same masks, the same variables (a dependency of the pack rule too). A
project with a literal version gives the literal; a lock that records none gives `""`.

`Pack` computes no dependencies: the nuspec states them, with tokens for their versions
([pack.md](pack.md#the-minimal-pack-from-a-projects-own-nuspec)). A package depending on another
group's package takes that version through its own token:

```xml
<dependencies>
  <dependency id="Acme.Core" version="$coreVersion$" />
</dependencies>
```

```fsharp
target "out/pkg/(name:*).nupkg" {
    let! name = getRuleMatch "name"
    let! d = Description.load (descriptionOf name)
    let fwks = [ for f in d.Frameworks -> f, outOf "Release" f name ]
    do! need [ for f, dir in fwks -> $"{dir}/{name}.dll" ]
    let! version = Locks.versionOf settings (lockOf name "Release") (fst fwks.Head)
    let! coreVersion = Locks.versionOf settings (lockOf "core" "Release") "netstandard2.0"
    // NuGet's NU5104: a stable package should not depend on a pre-release; Pack does not check it
    if not (version.Contains "-") && coreVersion.Contains "-" then
        failwithf "%s %s (stable) would depend on Acme.Core %s (pre-release)" name version coreVersion
    let tokens = Pack.tokensOf d version |> Map.add "coreVersion" coreVersion
    let! target = getTargetFullName ()
    let! options = Pack.Options.current ()
    Pack.fromNuspec target $"{dirOf name}/{name}.nuspec" tokens (Pack.libOf name fwks) options }
```

- With a suffix per group, `app 3.1.0-alpha.7` depends on `Acme.Core 1.4.0-beta.3`: publish both
  from the same build. `version="1.4.0-beta.3"` is NuGet's minimum version; write
  `[$coreVersion$]` for an exact one.
- A release of one group (empty suffix) over a pre-release of another is what `dotnet pack` warns
  about as NU5104. `Pack.fromNuspec` does not warn: the check above is the script's.
- Nothing ties a nuspec token to the right lock: a dependency read from the wrong lock packs
  without complaint.

## A valid suffix

The build checks the suffix of `VersionSuffixes` (`Locks.suffixProblem`) and fails, naming the
project, the mask and the reason, unless it is empty or a SemVer 2.0 pre-release label:

- dot-separated identifiers of `0-9A-Za-z-`, none empty: `ci.512`, `beta.3`, `alpha-7`; not
  `ci_512`, `feature/x`, `ci..1`, a leading or trailing `.`;
- no leading zero in a numeric identifier: `ci.0512` is invalid;
- no `+`: build metadata would be cut from the `deps.json` version but kept in
  `InformationalVersion` and `Locks.versionOf`, so the two would differ;
- no leading `-`: the build adds the dash (`-ci` would give `3.1.0--ci`, legal SemVer but not
  what was meant).

The masks of `Versions` give the whole version and are not checked.

Ordering: an identifier compares numerically only when it is all digits, so `ci.10` sorts after
`ci.9`, but `ci-10` sorts before `ci-9`. A dot in the label makes a SemVer 2.0 package, which
NuGet clients before 4.3 do not see.

## Messages

| Message | Where | What to do |
|---|---|---|
| `'<name>' (<cfg>\|<fwk>): the project sets its version itself (<v>); it is recorded as a literal, ...` (Info) | import | nothing, or `<VersionPrefix>` in the project |
| `'<name>' (<cfg>\|<fwk>): the version marker <m> is left where it is not the project's version <prefix>-<m> (<files>); ...` | import | build that text from `$(Version)`, not `$(VersionSuffix)` |
| `'<file>': the version marker <m> is left where the import knows no version (RecordOptions.VersionSuffix)` | import, `deps.json` | a `deps.json` layout the import does not know; report it |
| `the import is asked to record the version as a token (RecordOptions.VersionSuffix) and the script sets the property VersionSuffix too; drop one` | import | remove `VersionSuffix` from `RecordOptions.Properties` |
| `the version marker '<m>' (RecordOptions.VersionSuffix) must be letters and digits, starting with a letter` | import | use `Import.versionMarker` |
| ``'<name>': the lock '<lock>' needs a version ($(Version)) and no mask of `Versions` or `VersionSuffixes` matches '<path>' (<masks>)`` | build | add a mask; `**` last |
| ``'<name>': the version suffix '<s>' (`VersionSuffixes`, mask '<mask>') for '<path>' is not a SemVer pre-release label: <why>`` | build | fix the suffix ([A valid suffix](#a-valid-suffix)) |
| ``'<name>': the lock '<lock>' needs a version ($(Version)) and records no VersionPrefix for the suffix of `VersionSuffixes` ...`` | build | a `Versions` mask for that project, or re-import with the option |
| `the deps.json takes the version of '<file>' from its InformationalVersion, which '<path>' does not have` | build, application | the referenced output was built without one |
| `'<name>' (<fwk>): the lock '<lock>' is stale: '.../Directory.Build.props' changed since the import that wrote it; run the import again` | build | a new prefix: re-import, commit |
