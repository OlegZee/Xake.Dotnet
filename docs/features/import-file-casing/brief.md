# Brief: the import records the repository's spelling of each file

Status: draft for the owner's review, 2026-11-04. Branch `feature/import-file-casing` (from `dev`).
Line numbers are of `dev` at `68c786b`; `Xake:` marks the engine repository. Given (not re-opened):
the import records the repository's own spelling of each file, not the project file's.

## 1. The problem

The consumer's `.csproj`/`.fsproj` name files in another case than the repository has them
(`Foo\Bar.CS` for `foo/Bar.cs`). The import runs on macOS or Windows, whose file systems ignore
case, so msbuild accepts it and the import records `Foo/Bar.CS` in `xake.project.json` and the
locks. On Linux the same descriptions and locks fail:
- a lock's `Imports` entry (a miscased `<Import>` or project path) is "missing", so `staleOf`
  reports the lock **stale** and says "run the import again" (`Locks.fs:440-447`), which cannot
  help: the re-import on Linux fails in msbuild itself;
- a lock's `Sources`, `/resource:`, `/keyfile:`, `/analyzerconfig:` path makes csc/fsc fail
  (file not found); a recorded `App.Files`/own `App.config` path fails `Apps.write`'s
  `checkHash` with "is missing" (`Apps.fs:318-322`);
- a description's `sources` (F# files, C# literal masks), `resources[].file`, `keyFile`:
  the engine's scanner compares a literal segment ordinally everywhere but Windows
  (`Xake:src/core/PatternScan.fs:240-244`, `Fileset.fs:45`), so the file is not found;
- a project reference spelled `..\foo\foo.csproj` names the project `foo`, and the lock of
  `Foo` is looked up as `lockOf "foo"` (`Locks.fs:948-961`): not found on Linux.

### Evidence: the consumer's alias list (2026-11-04)

The consumer's Linux CI works around this today with a scan and a list of 62 case aliases (real
repository path -> the spelling something references), applied before the build. The aim of this
feature is to retire that list. By kind (counts of the list's entries):
- **resx file references** (`ResXFileRef` in a `.resx`): images, cursors, icons, 42 entries (the two `app.ico` may be `ApplicationIcon` instead)
  (`resources/copy.png` referenced as `Copy.png`, `hand1.cur` as `Hand1.cur`), including
  folder segments (`resources` vs `Resources`). The import records the resx; its content
  names these files, and the resx compile (`ResxFormat.parseFileRef`) reads them at build time.
- **doc-comment includes** in C# sources (`<include file='common.xml' .../>` for `Common.xml`,
  folders `Docs` vs `docs`): 12 entries; csc reads them, the spelling is in the sources.
- **msbuild-level paths**: `Directory.Build.props` imported as `Directory.build.props`,
  `.editorconfig` as `.editorConfig` (4 projects), one C# source (`*.Blazor.cs` vs `*.blazor.cs`),
  `readme.md`/`Readme.md` package files: what sections 2-3 cover.
So the larger part is referenced from **file content the import does not rewrite** (resx, C#
sources); decisions 7 and 8 below.

## 2. What the code does today

Every path below is taken from msbuild (item identity, property, command line, `-pp` import
list) as spelled, made absolute with `Path.GetFullPath` (no case correction), then written.

| Written to | Where it comes from | Under the repo? |
|---|---|---|
| description `project` | `MSBuildProjectDirectory` + name, `Import.fs:581`; the project path the script gives, `Import.fs:1919` | yes |
| description `sources`, F# | `Compile` identities, `Import.fs:482` | yes (a linked file may be outside) |
| description `sources`, C# | msbuild's default glob + literal `Compile Include/Remove` text, `Import.fs:209-324` (`yield prefix + slash mask`, `:323`; `definedIn`, `:246`) | yes |
| description `resources[].file`, `name` | `EmbeddedResource` identity, `relative`, `Import.fs:352-354`, `:412`; default name `ProjectPlan.manifestName` of that spelling (`ProjectPlan.fs:143`) | yes |
| description `keyFile` | `AssemblyOriginatorKeyFile`, `Import.fs:528` | yes |
| description `projectRefs`, `package.bundle` | `ProjectReference` file name, `Import.fs:532` | name of a repo file |
| description `package.icon/readme` | packed `None`/`Content` identity, `Import.fs:536-543` | yes |
| `Imported.Inputs`, lock `Evaluation.Imports` | msbuild's `-pp` import list, `repositoryFiles`, `Import.fs:680-692`, `:711`, `:1548` | yes (filtered) |
| lock `Evaluation.Project`, `Directory` | `ctx.ProjectFile/ProjectDir`, `Import.fs:1919`, `:1487`, `:1494`, `:1545` | yes |
| lock `Sources`, `Options` (`/resource:`, `/keyfile:`, `/analyzerconfig:`, `/additionalfile:`, ...) | `Csc/FscCommandLineArgs`, `CscArgs.absolutize`, `Import.fs:1366-1367` | repo files + `obj/` + packages + SDK |
| lock `Resources` (resx, compiled) | `EmbeddedResource` identity, `Import.fs:1463-1468` | resx in repo, `.resources` in `obj/` |
| lock `Generated` | msbuild's `obj/` files moved to `obj/xake/...`, `Import.fs:1416-1423` | generated, out of scope |
| lock `Recorded.Masks` | the description's masks made absolute, `Import.fs:1440-1458` | yes |
| lock `App.Content`, own `App.config`, `appSources` | `_XakeCopyItem`, `_XakeAppConfig`, `AppConfig`, `Import.fs:1262-1290`, `:1534-1541` | yes (package files hashed as packages) |
| lock `Recorded.Projects`, `project:<name>` | `MSBuildSourceProjectFile`, `Import.fs:1036-1058` (already compares ignoring case at `:1044`) | yes |
| legacy `Project.import` lock | `resolveAgainstProject`, `Project.fs:305-326`, imports `:342` | yes |

Out of scope: package folder, SDK, packs (msbuild resolves them from directory listings or
NuGet's lower-case layout, so their spelling is already the disk's), `obj/` files (msbuild
writes them with its own spelling, which the replay recreates). `Roots.tokenize` matches the
root ordinally (`Roots.fs:198-203`); a script whose project root itself is miscased is not this case.
`Git.fs` reads `.git` directly and never runs `git` (`Git.fs:9-12`).

## 3. The proposed change

One resolver, `Casing.ofRepository root path` (new, `src/hermetic`): for a path under the
project root, each segment below the root replaced by the repository's spelling; a path
outside the root, or with no match, unchanged. Applied where the paths enter (section 2): the
project files the script names (`Import.fs:1919`), `relative` and the F# list in `describe`,
the literal part of each C# mask (`MaskText.splitFixed`, `Roots.fs:44-49`; wildcard segments
kept, `!` excludes too), `repositoryFiles`, `CscArgs/FscArgs.absolutize` at `Import.fs:1366-1367`
(one composition covers `Sources`, `/resource:`, `/keyfile:`, the analyzer configs), the resx
of `Resources`, `appOf`, `projectsOf`, and the legacy `Project.fs` route.

**Recommended: (c)**, the git index for tracked files, the disk for the rest. One
`git ls-files -z --full-name` per repository and import (cached), a map lower-case path ->
spelling; a file not in the index (untracked, ignored, a generated file in the repo) resolved by
a directory walk, one cached listing per folder, names compared ignoring case.
Why the index: on macOS with `core.ignorecase=true`, a case-only rename on disk (`mv Foo.cs foo.cs`)
leaves the index at `Foo.cs`, and a Linux clone gets `Foo.cs`: the disk is then not the
repository's spelling. The resolver warns when the index and the disk disagree and records the index's.
- (a) git index only: misses untracked and generated files and folder-only lookups; no git, no fix.
- (b) directory walk only: no new dependency, but records the macOS disk spelling in the trap above.

Edge cases: two entries differing only by case (possible in the index, or on a case-sensitive
volume): the exact spelling wins, else the import fails naming both. A symlink keeps its own name
(the entry is listed, not resolved). Windows: the same resolver (NTFS ignores case), `\` read as
`/`. A folder segment in wrong case is corrected like a file name. Mask expansion
(`src/**/*.cs`) already yields the disk spelling (the scanner lists folders); only literal items
and the literal prefix of a mask need the resolver.

**Resource names.** msbuild names a resource after the item's spelling
(`CreateCSharpManifestResourceName`); `ResourceManager` lookups are case-sensitive. The recorded
name must stay msbuild's: the description writes `name` explicitly when the corrected `file`
would derive another (`Import.fs:412-426`, `:455-466`), the lock keeps msbuild's `/resource:path,name`.
A `/resource:` without a name (csc, fsc) takes the file name: the resolver must not change it.

## 4. Existing locks and descriptions

- No lock or description format change: paths are strings. A project whose files are spelled
  right gets byte-identical files (no churn); a miscased one changes paths (and may gain an
  explicit resource `name`) once, at one re-import by the consumer on macOS or Windows.
- Hashes do not change (same content); `xake.fingerprints` of the fsc self-build unaffected.
- A lock recorded before the change still fails on Linux. The replay should say why: where a
  recorded repository path is missing (`staleOf`, `Apps.checkHash`, the sources before compiling),
  look for a case-only match and fail with both spellings and "re-import with Xake.Dotnet >= X".
  No silent fallback: the lock names exact files.

## 5. Tests

- Resolver, every OS (temp folder, NUnit): `Foo/Bar.CS` -> `foo/Bar.cs`; wrong folder case;
  outside the root unchanged; missing unchanged; a symlink name kept; on a case-sensitive
  file system `foo/bar.cs` + `foo/Bar.cs`: exact wins, else the ambiguity error.
- Git part (CI has git): a temp repo, `git add Foo.cs`, the disk renamed `foo.cs`
  (`core.ignorecase=true`): the index spelling recorded, a warning.
- Import fixture (`fixtures/casing/`: an fsproj and a csproj naming `Src\A.FS`, `Strings.RESX`,
  `Key.SNK`, a miscased `<Import>` and `ProjectReference`): `Import.record`, then every repository
  path in the description and the lock is in `git ls-files` (or the exact disk listing); resource
  names equal msbuild's. Runs where the temp folder ignores case (probe), i.e. macOS and the
  Windows CI leg; `Assert.Ignore` on Linux (msbuild cannot import it there).
- Replay message: a lock with a miscased `Imports` path fails with both spellings (a pure test
  of the lookup on a listing; runs everywhere).

## 6. Decisions for the owner

1. Source of truth: git index for tracked files + directory walk for the rest (c), the walk
   alone (b), or the index alone (a)? [(c)]
2. How to read the index: run `git ls-files` at import time (the replay never needs git), or
   parse `.git/index` in `Git.fs` (no executable, but formats v2-v4)? [run `git`; without git
   or outside a repository, the walk alone, with one Info line]
3. When the project file's spelling differs from the repository's, does the import warn?
   [yes, one line per file at Info in the import log, like the "not in the description" lines; not an error]
4. Replay of an old miscased lock: fail naming both spellings, or fall back to the case-only
   match? And check the exact spelling on macOS/Windows too, so a dev machine catches it
   before Linux CI? [fail with both spellings, no fallback; exact check on every OS]
5. Resource names: keep msbuild's (from the project file's spelling) or derive from the
   repository spelling? [keep msbuild's, written explicitly in the description when they differ]
6. The legacy `Project.import` route (`Project.fs`): fix it too, or only `Import.record`/`project`?
   [both: one call at its `resolveAgainstProject` and args]
7. References inside a `.resx` (`ResXFileRef`), the bulk of the consumer's list: (a) the import
   resolves each referenced file to the repository's spelling and the lock records the pairs
   (resx spelling -> repository path, hashed as the resx's inputs), the resx compile uses them;
   (b) the resx compile itself looks the file up ignoring case (a replay-side fallback, against 4);
   (c) out of scope, the consumer renames the files or fixes the resx. [(a): the resx compile is
   ours, and the lock then names exact files]
8. Doc-comment `<include file=...>` in C# sources (csc reads them; the import cannot rewrite
   sources): out of scope, the consumer fixes the spelling in the sources (one-time, about a dozen
   references), or the import detects them and reports each one with both spellings? [report at
   import, Info, both spellings; the fix is the consumer's]

## 7. Owner's answers (2026-11-04)

Constraint: only **paths to files** change. A resource's name (msbuild's manifest name, the
`/resource:path,name` name, a `<data name>` inside a `.resx`) is never touched, and no file content
(resx, sources) is rewritten.

1. Source of truth: the **git index**.
2. Read it with `git ls-files` (or from files on disk); **never parse `.git/index` ourselves**.
3. Yes: the import warns about **every** difference between a project's spelling and the repository's.
4. No case-insensitive lookup at replay. An old miscased lock fails as today ("lock ... stale",
   checked before anything is built); the fix is a re-import, not a fallback.
5. Resource names are not touched (see the constraint).
6. Yes: the legacy `Project.import` route too.
7. `ResXFileRef` in a `.resx`: the import resolves each referenced file through the git index; the
   lock records the pairs (the resx's spelling -> the repository path, hashed as the resx's inputs);
   the resx compile reads the file at the recorded path. The `.resx` itself is not rewritten; no
   case-insensitive lookup at replay.
8. `<include file=...>` in C# doc comments: the import finds them and warns about each one with both
   spellings; the fix is the consumer's, in the sources.
