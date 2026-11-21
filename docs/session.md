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

- `Xake.Dotnet` 5.0.0 (2026-11-21, `feature/scripts-xake5`, `docs/features/scripts-xake5/`): the
  port to Xake 5 (`docs/features/xake5/`, PR #24, published as `5.0.0-preview.1` on
  `[5.0.0-preview.2, 6.0)`), the floor raised to the released engine, `XakeVersion`
  `[5.0.0, 6.0)`, and both build scripts on Xake 5.0.0. Goes to `dev` by squash; the owner tags
  `v5.0.0` on that commit (`publish.yml` pushes the package) and `master` follows by `--ff-only`.
- `Xake.Dotnet` 4.4.0 (2026-11-19), squash-merged into `dev` one PR at a time: SDK 11 runner
  images (#17, `docs/features/sdk11/`), declared inputs of generated binaries (#16,
  `lock-inputs`), the `deps.json` version from the lock (#18, `deps-version`), Windows paths in
  recorded analyzer configs (#19, `editorconfig-paths`), .NET Framework reference assemblies from
  the package (#20, `netfx-refasm`), the legacy projects recipe and guard (#21,
  `legacy-recipe`), the source order independent of the importing OS (#22, `source-order`).
  Previews `v4.4.0-preview.1` (`b82100b`) and `v4.4.0-preview.2` (`2f89bfd`) were checked by a
  consumer on macOS and a Windows VM: Windows locks equal the macOS ones. Tag `v4.4.0` on the
  release-notes merge; `publish.yml` pushes the package; `master` follows by `--ff-only`.
  `feature/import-file-casing` (#12) is not in it; if it lands, it is 4.5.0 (its notes still say
  4.2.0).
- `Xake.Dotnet` 4.3.0 (satellite assemblies, `docs/features/satellites/`): PR #15 squash-merged
  into `dev`, tag `v4.3.0` on that merge (2026-11-08); `publish.yml` pushes the package;
  `master` follows by `--ff-only`. New package dependency `System.Reflection.Metadata` 8.0.0.
- `Xake.Dotnet` 4.2.0 (the version as a token in a recorded lock,
  `docs/features/lock-version-params/`): PR #13 squash-merged into `dev`, tag `v4.2.0` on that
  merge (`22d61e0`, 2026-11-07); `publish.yml` pushed the package, on nuget.org the same day.
  `master` follows by `--ff-only`.
- `Xake.Dotnet` 4.1.0 (WPF projects recorded, `docs/features/wpf-record/`): PR #9 merged into
  `dev`, tag `v4.1.0` on that merge (2026-11-06); `publish.yml` pushes the package.
- Xake 4.0.0 is on nuget.org (2026-11-03). `release/4.0.0` moves the floor to `[4.0.0, 5.0)`,
  writes the 4.0.0 notes (`docs/release-notes.md`) and goes to `dev`; the owner then merges `dev`
  into `master` and tags `v4.0.0` (`publish.yml` pushes `Xake.Dotnet` 4.0.0 with the repository
  secret `NUGET_API_KEY`). Not published yet.
- After the publish (S9): `build.fsc.fsx` on `#r "nuget: Xake.Dotnet, 4.0.0"`; deprecate and
  unlist `Xake.Hermetic.Dotnet` and the 1.1.x betas of `Xake.Dotnet` on nuget.org.
- `bump-base` keeps the UTF-8 BOM of `src/dotnet/Xake.Dotnet.fsproj` (it used to drop it).

## Build facts

- The scripts run on Xake 5 (2026-11-21): the root's `.xake` and `.xake.fsc` are database 0.7. A
  checkout switched back to a branch on Xake 4 recreates them (Xake 4 cannot read 0.7; a full
  rebuild, no failure).
- `build.fsx` bootstraps on `Xake 5.0.0`; `build.fsc.fsx` on `Xake 5.0.0` + `Xake.Dotnet
  5.0.0-preview.1` (the newest published 5.x); it moves to `Xake.Dotnet 5.0.0` after the release.
  `testjobs.fsx` has no `#r` of its own (loaded by both). The samples pin `Xake 5.0.0`.
- `bump-base` still does not re-resolve Xake in the locks (checked again on the
  `Xake.Dotnet 5.0.0-preview.1` bootstrap, 2026-11-21, floor 5.0.0): it moves the floor and the
  description (keeping the BOM), then run `update-locks -d LOCK_REFRESH=Xake -d NUGET_FETCH=on`.
  The `LOCK_REFRESH` environment variable it sets does not reach the lock rule, which reads the
  build variable (`Var.read`); not fixed here (out of scope of the release).
  On a machine without SDK 10.0.403 a lock records its own SDK's compiler: put the `Compiler`
  lines back and recompute `xake.fingerprints` (`docs/fingerprints.md`).
- `src/hermetic` cannot be a project reference of `src/dotnet` (it references `src/dotnet`): the
  `IncludeHermeticOutput` target of `src/dotnet/Xake.Dotnet.fsproj` builds it with an explicit
  `MSBuild` call while packing and adds its dll and xml to `lib/<tfm>/`.
- The `Xake` package carries `Xake.dll` only from `4.0.0-preview.2` (Xake S-D14); earlier ones
  also carried an older `Xake.Dotnet.dll`. The hermetic code uses the pattern module (`PatternAst`,
  `MaskText`, Xake #131), first in `4.0.0-preview.3`; the `XakeVersion` floor is `4.0.0` (release 4.0.0).
- The test rules (`test`, `test-build::<project>`, `test::<project>/<slice>`) are typed
  commands in `testjobs.fsx`, shared by both scripts; a command always runs, so `test` never
  skips a stale `dotnet build` (the old `.xake` trap is gone).
- The test projects share `src/dotnet`: `test-restore` restores them one after another, then
  `test-build::<project>` builds with `--no-restore` (two restores at once raced on
  `src/dotnet/obj/project.assets.json`, seen in CI's `xake-latest`).
- The locks name the SDK compiler by version: SDK 10.0.403, pinned in `build.yml`'s `build-fsc`.
- The hermetic tests that describe "the repository's own projects" (`DescriptionImportTests`,
  `DescribedTests`, `LocksTests`, `BuildFromLockTests`) use `src/dotnet` and `src/hermetic`
  with the engine as the package `Xake`; they need nuget.org (the `Xake` package) to restore.

## Feature folders

- Released in 4.4.0 (2026-11-19); open only the "Later" items of their `tracker.md`:
  `docs/features/sdk11/`, `lock-inputs/` (post-compile outputs through the same declaration),
  `deps-version/`, `editorconfig-paths/`, `netfx-refasm/`, `legacy-recipe/`, `source-order/`
  (test projects on Windows: testhost x64 in `App/Content`).
- Open observation (2026-11-19, a consumer's Windows re-import): a lock whose only difference
  would be the importing SDK's patch version (`"Sdk": "8.0.427"` vs 8.0.428) is not rewritten;
  check whether that is intended and document it.
- `docs/features/satellites/`: released in 4.3.0; open only its `tracker.md` "Later" items (run
  folders with a referenced lock's satellites, F# satellites from a lock).
- `docs/features/lock-version-params/`: released in 4.2.0; open only the "Later" items of its
  `tracker.md` (`FileVersion` as a token, a `Pack` helper for dependency versions).
- `docs/features/hermetic-windows/`: the remaining work of the hermetic-on-Windows findings (moved from
  Xake on 2026-11-03); start with its `session.md`, "Next step".
