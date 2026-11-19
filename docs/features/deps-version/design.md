# Design: the project version in `deps.json` from the lock

Status: approved by the owner 2026-11-19; implemented as designed. Code references are to `b4853da` (4.3.0).

## What the code does today

`Apps.depsResolved fileOf text` (`Apps.fs:296-307`) replaces `$(Version:<file>)` (written by
`Import.depsVersionTokens` for the application itself and every referenced project) with
`informationalVersion (fileOf file)`: the built file's `ProductVersion` without `+<sha>`. No
attribute -> `failwithf`. `Apps.write` (its only caller, from `Locks.compileAssembly`) has the
application's lock entry but neither the referenced projects' locks nor `versionIn`; `Apps` is
compiled before `Locks`.

## The gap the design has to close

`Locks.compile settings lockPath fwk` knows only its own lock. A referenced project's lock path is
the script's `lockOf name cfg`; the application's entry records each referenced project as
`Recorded.Projects: ProjectOutput { Name; Configuration; Framework; Path = <out>/<file>.dll }`
(transitive ones included: they come from `ReferencePath`), but not where its lock is.

## Resolution path

1. **`Locks.Settings.LockOf: string -> string -> string`** (new field), the lock of a project by
   name and configuration, as `Import.RecordOptions.LockOf`; `Default` is the same as there,
   `locks/<name>/<cfg>.json` (relative to the project root). A script with that layout changes
   nothing; another one sets it once, the same function it already hands `needAll`/`generate`.
2. In `Locks.compileAssembly`, for an application whose recorded `deps.json` carries
   `$(Version:`, before `Apps.write`: a map `<file> -> Result<version, why>`, keyed by file name
   (ignoring case):
   - the application's own output (`outputName entry`): its own entry;
   - each `Recorded.Projects` item (`Path.GetFileName p.Path`): the entry for `p.Framework` of
     `settings.LockOf p.Name p.Configuration`.
   An entry whose `Version` property carries `$(Version)` and that records `VersionPrefix` gives
   `versionIn settings root lock entry` (the same recipe `compile` and `versionOf` use: a
   `Versions` mask, else prefix + `-<suffix>`), build metadata (`+...`) dropped as the
   `deps.json` drops it. Otherwise `Error` with what was looked for: "no lock '<path>'
   (`Locks.Settings.LockOf`)" or "the lock '<path>' records no VersionPrefix".
3. `Apps.write` takes the map as a function `versionOf: string -> Result<string, string>`
   (`Apps` stays pure, no lock loading there) and hands it to `depsResolved`.
4. `depsResolved` for `Version`: `Ok v` -> `v`, the file is not read. `Error why` (or a file not
   in the map) -> **fallback**: `informationalVersion path` as today.

The map is built only when a `deps.json` has the token, so applications without it (and locks
from before 4.2.0) do not load other locks or run suffix recipes.

## The error (no version at all)

    '<app>' (<fwk>): the deps.json needs the version of '<file>' and has none: <why>, and
    '<path>' has no InformationalVersion

e.g. `'app' (net8.0): the deps.json needs the version of 'plain.dll' and has none: the lock
'locks/plain/Release.json' records no VersionPrefix (import it with RecordOptions.VersionToken),
and '/r/out/Release/net8.0/plain/plain.dll' has no InformationalVersion`. A failing `versionIn`
(a suffix that is no SemVer label, no mask matching) fails as it does for `compile`, with its own
message: such a project's own compile fails the same way when it stamps the version.

## Point 4: `SourceRevisionId` (no msbuild evaluation needed)

The SDK writes `$(Version)` into `deps.json` (`GenerateDepsFile`'s `ProjectVersion`); the
`+<sha>` is appended only to `InformationalVersion`
(`IncludeSourceRevisionInInformationalVersion`), never to `Version`. The lock's `$(Version)` is
recorded from that same `Version` (`<prefix>-<marker>` -> prefix + token, `Import.fs:1734-1742`),
and `depsVersionTokens` tokenized the same `<prefix>-<marker>` in `deps.json`: lock value and
reference value are the same property by construction. `SourceRevisionId` only made today's path
need the `+` strip. Where they could differ, the lock is the right one: a project with
`GenerateAssemblyInfo=false` and a hand-written `InformationalVersion("2.0.0")` gets `1.0.0-<s>`
in the SDK's `deps.json`, while 4.3.0 wrote `2.0.0`. So no change in `Import.fs`.

## Byte-for-byte with 4.3.0

For a project with the generated attribute: `InformationalVersion` = `$(Version)` resolved by the
same `versionIn` + `+sha`, stripped -> the same string. Differs only where 4.3.0 was wrong (a
hand-written attribute that is not `$(Version)`).

## Tests (`src/hermetic.tests`)

- `AppTests` (pure): `depsResolved` with `Ok` and a stand-in file that is no assembly (nothing
  reads it); `Error` + an assembly with the attribute -> fallback; `Error` + none -> the message
  names the app, the file, the lock and the path.
- `RecordTests` "Import record version token": one test with a project `plain`
  (`GenerateAssemblyInfo=false`, `VersionPrefix` 2.2.0) referenced by an app: it builds, its
  `deps.json` names `plain/2.2.0-<suffix>`; another suffix (`-d`) changes it; the existing
  `deps.json` assertions of the fixture stay as they are (4.3.0 output).
- A lock without `VersionPrefix`: the existing fixtures with literal versions keep passing
  (fallback), plus the pure test above.

## Line budget

| where | lines |
|---|---|
| `Locks.fs` (`Settings.LockOf` + default, the map in `compileAssembly`) | ~30 |
| `Apps.fs` (`depsResolved`/`write` signature, fallback, error) | ~15 changed |
| tests (`AppTests` ~35, `RecordTests` ~40) | ~75 |
| docs (`lock.md` Settings/apps, `versioning.md`, `README.md` Settings line, release notes) | ~30 |

## Breaking

`Locks.Settings` gains a field: a script that writes the whole record (not `Default with`)
must add `LockOf`. None in this repository or its docs does.

## Found while implementing

- `Error` in `src/hermetic` is Xake's `Level.Error`: the code writes `Result.Error`.
- The application's rule now depends on a referenced project's suffix through the suffix recipe
  it runs (`versionIn` in the application's rule records the variables the recipe reads), not
  through the referenced file: a project with `GenerateAssemblyInfo=false` gets the same bytes
  under another suffix. A suffix from a variable (`-d`, `getVar`, `Var`) rebuilds the
  application; the end-to-end test uses `getVar` for that reason.
