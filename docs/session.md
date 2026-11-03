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

## Build facts

- `build.fsx` bootstraps on `Xake 4.0.0-preview.3`; `build.fsc.fsx` on `Xake 3.15.0` +
  `Xake.Hermetic.Dotnet 0.10.0` until `Xake.Dotnet` 4.0.0 is published (then one
  `#r "nuget: Xake.Dotnet, 4.0.0"`).
- `src/hermetic` cannot be a project reference of `src/dotnet` (it references `src/dotnet`): the
  `IncludeHermeticOutput` target of `src/dotnet/Xake.Dotnet.fsproj` builds it with an explicit
  `MSBuild` call while packing and adds its dll and xml to `lib/<tfm>/`.
- The `Xake` package carries `Xake.dll` only from `4.0.0-preview.2` (Xake S-D14); earlier ones
  also carried an older `Xake.Dotnet.dll`. The hermetic code uses the pattern module (`PatternAst`,
  `MaskText`, Xake #131), first in `4.0.0-preview.3`, so the `XakeVersion` floor stays at preview.3 or later.
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
