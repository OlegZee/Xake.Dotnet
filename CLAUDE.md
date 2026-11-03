# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Session Context

Always read `docs/session.md` at the start of every session, unconditionally.

Then read the **feature folder** for the current branch: for a branch `feature/<name>` (or
`bugfix/<name>`), the folder is `docs/features/<name>/`. It holds the working state of that
feature and is the context to load:

- `session.md` — where things stand and the exact next step; update it before ending a session
- `tracker.md` — one-line work items with status; keep it current as work lands
- `brief.md` (optional) — the design/product brief; decisions live here, do not re-litigate them

If the folder does not exist for a feature branch, create it at the first session on that
branch. `docs/session.md` keeps only what is cross-feature (facts, release notes, traps).

## Project Overview

The .NET tasks of the Xake build tool, built on the published `Xake` package (the engine lives
in https://github.com/OlegZee/Xake). One NuGet package, `Xake.Dotnet`, carries two assemblies:
`Xake.Dotnet` (`src/dotnet`: `csc`, `fsc`, `msbuild`, `resgen`, project descriptions, toolchain
discovery) and `Xake.Hermetic.Dotnet` (`src/hermetic`: locks, restore, SBOM, verify, pack, sign).

## Build & Test Commands

```bash
# Build, test, pack (primary command)
dotnet fsi build.fsx -- -- build test pack

# One test project / one job (testjobs.fsx)
dotnet fsi build.fsx -- -- test-project::dotnet
dotnet fsi build.fsx -- -- test::hermetic/lock

# Filtered tests
dotnet fsi build.fsx -- -- test -d FILTER=TestName
dotnet test src/dotnet.tests --filter "Name~\"some pattern\""

# The fsc self-build from the committed descriptions and locks (SDK 10.0.403)
dotnet fsi build.fsc.fsx -- -- build check -d NUGET_FETCH=on
```

Requires .NET SDK 8.0+ (see `global.json`).

## Project Layout

- `src/dotnet/` — `Xake.Dotnet.fsproj`, the packable project (adds the hermetic assembly to its package)
- `src/hermetic/` — `Xake.Hermetic.Dotnet.fsproj`, not packable on its own
- `src/dotnet.tests/`, `src/hermetic.tests/` — NUnit tests, net8.0
- `src/XakePackage.targets` — drops the `Xake.Dotnet.dll` that `Xake` packages up to the 4.0 split still carry
- `build.fsx` — msbuild-driven build; `build.fsc.fsx` — the declarative fsc build (`locks/`, `xake.fingerprints`)
- `testjobs.fsx` — the test slices both scripts run
- `tools/import-from-xake.py` — the history import from Xake (do not change its path rules)
- `docs/` — task, description and hermetic docs; `samples/` — example scripts

## Development Workflow

- Feature branches merge to `dev` via PR with squash (the history-import PRs with a merge commit)
- Releases: merge `dev` to `master`, tag `vX.Y.Z` (triggers the NuGet publish of `Xake.Dotnet` X.Y.Z)
- No customer or company names anywhere in this public repository (files, commits, PRs)
