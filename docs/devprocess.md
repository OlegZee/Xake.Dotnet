# Development process

How Xake.Dotnet is built, tested and released. The engine's process is in
[Xake's devprocess](https://github.com/OlegZee/Xake/blob/dev/docs/devprocess.md); this file
covers what differs here.

## Requirements

- .NET SDK 8.0 or newer. `global.json` pins `8.0.0` with `rollForward: latestMajor`.
- For `build.fsc.fsx` and its CI job: SDK **10.0.403** exactly (the locks name its compiler).

## Branch workflow

1. Create a feature branch from `dev`, push it, open a pull request to `dev`
2. Wait for the checks (`build`, `build-fsc`, `xake-latest`, `windows`), fix, merge with squash

The history-import pull requests (`tools/import-from-xake.py`) are merged with **"Create a merge
commit"**: a squash would drop the imported history, a rebase would rewrite it.

## Build and test

```bash
dotnet fsi build.fsx -- -- build test pack
```

- `build` compiles `src/dotnet` and `src/hermetic` into `out/<fwk>/` (msbuild, `dotnet build`).
- `test` runs the jobs of `testjobs.fsx` in parallel: `dotnet/server`, `hermetic/server`
  (serial, the compiler server is one per machine), `dotnet/jobs`, `dotnet/rest`,
  `hermetic/record|run|lock|rest`. Each job writes its trx and `console.log` into
  `out/test/<project>/<slice>/`. One job alone: `test::hermetic/run`; one project:
  `test-project::dotnet`. `-d FILTER=<regex>` narrows every job.
- `pack` builds the one package `out/pkg/Xake.Dotnet.<version>.nupkg` and checks it: a
  dependency on `Xake`, none on `Xake.Hermetic.Dotnet`, `Xake.Dotnet.dll/.xml` and
  `Xake.Hermetic.Dotnet.dll/.xml` in `lib/net462/` and `lib/netstandard2.0/`, no `Xake.dll`.
- `-d XAKE_VERSION=<version or range>` builds and tests against another `Xake` than the
  project file's range (CI's `xake-latest` uses `4.*-*`).

The test projects reference `src/dotnet` (and `src/hermetic`); the engine reaches them through
`src/dotnet`'s package reference.

## Versions

- **One package, one version line**: tag `vX.Y.Z` = package `Xake.Dotnet` X.Y.Z, both
  assemblies. The major follows the `Xake` major it targets (4.x on `Xake [4, 5)`); minors and
  patches are this repository's own.
- **The hermetic part is marked "API may change"**: an incompatible change in it is a minor
  version with a release-notes line, until that mark is removed.
- **The `Xake` range** is the `XakeVersion` property of `src/dotnet/Xake.Dotnet.fsproj`
  (`[<floor>, 5.0)`). The floor is the version this repository builds against; it moves only
  when the code needs a newer engine:

  ```bash
  dotnet fsi build.fsc.fsx -- -- bump-base -d XAKE_FLOOR=4.0.1   # default: the newest on nuget.org
  ```

  It refuses a version nuget.org does not list, rewrites the floor, re-imports the descriptions
  and refreshes the locks for `Xake` only; commit the project file, descriptions, locks and
  `xake.fingerprints` together.

## The fsc build (`build.fsc.fsx`)

The libraries compiled by fsc from committed files, no msbuild for them: the descriptions
`src/*/xake.project.json` (written by `import` from the project files), the locks
`locks/<name>/Release.json` (written by `update-locks`), `xake.fingerprints` (one line per
description and lock). `build` compiles from them, `check` fails on a stale fingerprint.

- After a project-file change (a source file added, a package or the range moved): `import`
  **and** `update-locks`, then commit descriptions, locks and fingerprints, or CI's `build-fsc`
  fails.
- The locks name the SDK compiler by the SDK version: another SDK needs `update-locks` and the
  matching pin in `build.yml`.
- The script bootstraps on published packages (its first lines), separate from what it builds.

## Release

`publish.yml` runs on a tag `v*`: version = tag minus `v`, `build test pack`, then `dotnet nuget
push` of the one package with the `NUGET_API_KEY` secret and `--skip-duplicate`. A broken
release is replaced by the next patch, never re-published.

```bash
git checkout master && git pull && git merge --ff-only dev
git tag vX.Y.Z && git push origin master --tags
```

Checklist: `build test pack` and `build.fsc.fsx build check` green; the nuspec has `FSharp.Core`
8.0.100 and `Xake [<floor>, 5.0)`; release notes have the entry; `samples/fullframework.fsx`
runs after `build`.

### Bootstrapping note

`build.fsx` starts with `#r "nuget: Xake, <version>"`, `build.fsc.fsx` with the packages it
needs; both are published versions, bumped only after a release lands on nuget.org. To work
against an unreleased engine, pack Xake into a folder at a version nuget.org does not have and
pass the folder as an extra restore source for that shell only (for example
`export RestoreAdditionalProjectSources=/tmp/xake-feed`), with `NUGET_PACKAGES` pointing at a
scratch folder so the build does not shadow the release in `~/.nuget/packages/`. Nothing of it
goes into a commit.
