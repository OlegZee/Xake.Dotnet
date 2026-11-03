# Session notes

Cross-feature facts and traps of this repository; feature state lives in `docs/features/<name>/`.
The engine's notes are in [Xake's docs/session.md](https://github.com/OlegZee/Xake/blob/dev/docs/session.md).

## Origin

- The code came from Xake with its history, cut by `tools/import-from-xake.py` (a
  `git fast-export`/`fast-import` path filter, deterministic: the same Xake commit gives the same
  SHAs). The first cut (split step S4) was at Xake `af45852` (tag `v4.0.0-preview.1`): 617 Xake
  commits, 155 kept, 134 files. A later cut from Xake `dev` contains those 155 commits unchanged
  and merges on top as a normal merge; the path rules, the subject rewrite (a trailing `(#N)`
  and `Merge pull request #N from` -> `OlegZee/Xake#N`), the mapping of the owner's work
  address to the GitHub noreply address and the neutral names in old file versions (words
  matched by hash, `NAMES`) must not change between cuts. The cut tip itself needs no
  replacement: the filter's tree check fails if it does. No branch is kept for
  the cut: rerun the filter in a scratch clone and merge the fetched ref.
- Import PRs are merged with a merge commit, never squash or rebase.
- The 1.x code of 2018 is on `master` up to the 1.x tip and under `v1.1.0`, `v1.1.4`,
  `legacy-1.x`.

## Release state

- Xake 4.0.0 is on nuget.org (2026-11-03). `release/4.0.0` moves the floor to `[4.0.0, 5.0)`,
  writes the 4.0.0 notes (`docs/release-notes.md`) and goes to `dev`; the owner then merges `dev`
  into `master` and tags `v4.0.0` (`publish.yml` pushes `Xake.Dotnet` 4.0.0 with the repository
  secret `NUGET_API_KEY`). Not published yet.
- After the publish (S9): `build.fsc.fsx` on `#r "nuget: Xake.Dotnet, 4.0.0"`; deprecate and
  unlist `Xake.Hermetic.Dotnet` and the 1.1.x betas of `Xake.Dotnet` on nuget.org.
- `bump-base` keeps the UTF-8 BOM of `src/dotnet/Xake.Dotnet.fsproj` (it used to drop it).

## Build facts

- `build.fsx` bootstraps on `Xake 4.0.0`; `build.fsc.fsx` on `Xake 4.0.0` +
  `Xake.Dotnet 4.0.0-preview.1` (published 2026-11-03 to try the package before 4.0.0); moves to
  `Xake.Dotnet 4.0.0` after the release (split step S9).
- `src/hermetic` cannot be a project reference of `src/dotnet` (it references `src/dotnet`): the
  `IncludeHermeticOutput` target of `src/dotnet/Xake.Dotnet.fsproj` builds it with an explicit
  `MSBuild` call while packing and adds its dll and xml to `lib/<tfm>/`.
- The `Xake` package carries `Xake.dll` only from `4.0.0-preview.2` (Xake S-D14); earlier ones
  also carried an older `Xake.Dotnet.dll`. The hermetic code uses the pattern module (`PatternAst`,
  `MaskText`, Xake #131), first in `4.0.0-preview.3`; the `XakeVersion` floor is `4.0.0` (release 4.0.0).
- Trap: `build.fsx`'s `test-build::<project>` is a phony rule with no file dependencies; an
  old `.xake` database can skip it and run the tests of a stale build. Delete `.xake` (or
  `clean`) after editing tests outside a full rebuild.
- The locks name the SDK compiler by version: SDK 10.0.403, pinned in `build.yml`'s `build-fsc`.
- The hermetic tests that describe "the repository's own projects" (`DescriptionImportTests`,
  `DescribedTests`, `LocksTests`, `BuildFromLockTests`) use `src/dotnet` and `src/hermetic`
  with the engine as the package `Xake`; they need nuget.org (the `Xake` package) to restore.

## Feature folders

- `docs/features/hermetic-windows/`: the remaining work of the hermetic-on-Windows findings (moved from
  Xake on 2026-11-03); start with its `session.md`, "Next step".
