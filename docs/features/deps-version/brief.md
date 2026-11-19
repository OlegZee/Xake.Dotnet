# Brief: project version in `deps.json` from the lock, not from `InformationalVersion` (Xake.Dotnet, after 4.3.0)

Audience: the agent working in the Xake.Dotnet repository. Xake.Dotnet is public: this brief names no product or
company of the consumer. Code references are to v4.3.0 (`src/hermetic`, commit `b4853da`).

## Problem

An application's `deps.json` names its own project and project references with a version. The lock stores it as the
token `$(Version:<file>)`, and `Apps.depsResolved` (`Apps.fs:296–304`) resolves it by reading
`AssemblyInformationalVersionAttribute` from the built assembly. When the attribute is missing the build throws.

It is missing for any project with `GenerateAssemblyInfo=false` and no hand-written `AssemblyInfo` carrying it. The
consumer has one such project and works around it with an import overlay that injects an
`[assembly: AssemblyInformationalVersion("$(Version)")]` compile item. The workaround changes the assembly compared
to the reference build (an extra attribute), only to feed `deps.json`.

## What to do

1. **Source of truth = the lock.** The version token is already resolved for generated sources: `$(Version)` =
   the entry's `VersionPrefix` + the run's suffix (`Locks.Settings.VersionSuffixes`, the same recipe `compile` and
   `Locks.versionOf` use). `depsResolved` resolves `$(Version:<file>)` the same way, from the lock entry of `<file>`.
2. **Fallback.** Only when that entry has no `VersionPrefix` (locks recorded before version tokens): read
   `InformationalVersion` from the assembly as today.
3. **No version at all** → an error naming the application, the referenced project and what was looked for
   (`VersionPrefix` in its lock, `InformationalVersion` in `<path>`), not a reflection exception.
4. The value must equal what the reference SDK build writes: for an SDK build that is `$(Version)` of the
   project, which with the usual setup equals `InformationalVersion` without the `+<sha>` part. Check that
   `SourceRevisionId` / `IncludeSourceRevisionInInformationalVersion` does not make them differ; if it can, take
   the `deps.json` value from the msbuild evaluation at import (the project's `Version` property) and document why.

## Acceptance

- An app referencing a project with `GenerateAssemblyInfo=false` and no `InformationalVersion` builds; its
  `deps.json` names that project with `<prefix>-<suffix>` of this run.
- For projects that do have the attribute, `deps.json` is byte-for-byte as with 4.3.0 (same run suffix).
- A lock without `VersionPrefix` still works through the fallback.
- Changing the run suffix changes `deps.json` and nothing reads the assembly to get it (verifiable by a test with a
  stub assembly lacking the attribute).
- Consumer side (not this repository): the overlay section that injects the attribute is removed and a re-import gives
  the same `deps.json`.

## Decision (2026-11-19)

The owner approved `design.md` (`e5706b1`) as proposed: the version from the referenced project's
lock found by a new field `Locks.Settings.LockOf` (default `locks/<name>/<cfg>.json`), the
`InformationalVersion` fallback, the error naming both, no change in `Import.fs` (point 4 needs
no msbuild evaluation).
