# Consumer check: a real solution recorded with the version token

The tracker item "two brands, 93 projects": the owner runs it on a consumer solution with the
local preview package. Three checks: the first import with the option changes each entry once
(and only by the version), a second import changes nothing, a build with two suffix groups
stamps the right versions. Report back what differs from the expectations below.

## 0. The preview package

In this repository (worktree of `feature/lock-version-params`):

```bash
dotnet fsi build.fsx -- -- pack -d VERSION=4.2.0-version.1
```

It writes `out/pkg/Xake.Dotnet.4.2.0-version.1.nupkg`. In the consumer's script, take it from
that folder ahead of nuget.org:

```fsharp
#i "nuget: /absolute/path/to/Xake.Dotnet/out/pkg"
#r "nuget: Xake.Dotnet, 4.2.0-version.1"
```

`dotnet fsi` caches packages by version: after a repack with the same version, clear
`~/.nuget/packages/xake.dotnet/4.2.0-version.1` first.

## 1. The first import with the option

Start from a clean tree with the locks of the current import committed. Turn the option on in
the import command (nothing else changes):

```fsharp
{ Import.RecordOptions.Default with
    LockOf = lockOf; OutOf = outOf
    VersionToken = true }
```

Do not pass `VersionSuffix` in `RecordOptions.Properties` (the import refuses both). A `VERSION`
in the environment is emptied for this import, as it should be.

Run the import, then:

```bash
git diff --stat -- locks/
git diff -- locks/ | grep '^[-+] ' | grep -v -E 'Version|\$\(Version' | head
grep -rl xakeversionmarker locks/ || echo "no marker left"
```

Expected:

- each entry of each lock changes; the second command prints nothing (no change other than the
  version), and no marker is left;
- per entry: `"Version": "$(Version)"`, a new `"VersionPrefix": "<prefix>"`, and
  `AssemblyInformationalVersionAttribute("$(Version)...")` in the generated `AssemblyInfo`;
  `AssemblyVersionAttribute` and `AssemblyFileVersionAttribute` unchanged (`<prefix>.0`);
- each application's recorded `deps.json` (`<lock>.files/<tfm>/app/<app>.deps.json`): every
  project version becomes `$(Version:<file>.dll)`; package versions unchanged;
- a project that sets `<Version>` itself: unchanged, and an `Info` line "the project sets its
  version itself" in the import log. Note which ones (and whether that was intended);
- a WPF entry: its BAML (`<lock>.files/<tfm>/<name>.g.resources`) and `Recorded.Markup`
  unchanged.

A failure "the version marker ... is left where it is not the project's version" names a
generated file built from `$(VersionSuffix)` on its own: note the project and the target that
writes it.

Commit the locks (locally) before step 2.

## 2. A second import

Run the same import again, then:

```bash
git status --short -- locks/
```

Expected: empty. Also try it with a `VERSION` exported (as a release workflow does):
still empty.

## 3. A build with two suffix groups

In the build script's `Locks.Settings`, one group per brand (masks relative to the project
root; the first matching wins, `**` last):

```fsharp
let settings =
    { Locks.Settings.Default with
        VersionSuffixes =
            [ "<brand-a folder>/**", recipe { let! s = vars.Suffix in return s }
              "**",                  recipe { let! s = vars.Suffix in return (if s = "" then "b" else s + "-b") } ] }
```

with a script variable `Suffix` (e.g. `Var.string (name = "SUFFIX") |> withDefault "local"`).
`Locks.compile settings`, `Locks.doc settings` and the application rules all take the same
`settings`; a package takes its version from `Locks.versionOf settings lockPath fwk`.

Build three times:

```bash
dotnet fsi build.fsx -- -- build -d SUFFIX=ci.1
dotnet fsi build.fsx -- -- build -d SUFFIX=ci.1
dotnet fsi build.fsx -- -- build -d SUFFIX=ci.2
```

Expected:

- build 1: a brand-a assembly has "Product version" `<prefix>-ci.1`, the others
  `<prefix>-ci.1-b`; "File version" `<prefix>.0` everywhere; an application's `deps.json` names
  each project with that project's version (the group of the project, not of the application);
  no `$(` left in any written `deps.json`;
- build 2: nothing compiled;
- build 3: every tokenized assembly compiled again, with `ci.2`;
- a suffix with `+` or `_` (`-d SUFFIX=ci+1`) fails with "is not a SemVer pre-release label";
- removing the `**` mask fails with "needs a version ($(Version)) and no mask ... matches".

To read the product version of a file:

```bash
echo 'printfn "%s" (System.Diagnostics.FileVersionInfo.GetVersionInfo(fsi.CommandLineArgs.[1]).ProductVersion)' > pv.fsx
```

```bash
dotnet fsi pv.fsx path/to/X.dll
```

## What to report

The `--stat` line count, any non-version line in the diff, the `Info` projects, anything left in
`git status` after step 2, the versions of one assembly per group and one `deps.json` excerpt
from step 3, and the time of each build.
