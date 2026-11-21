# Session: scripts-xake5

Branch `feature/scripts-xake5`, cut from `origin/dev` `96f36de` (the Xake 5 port, PR #24,
published as `Xake.Dotnet 5.0.0-preview.1`). Started 2026-11-21. Goal: the `Xake.Dotnet` 5.0.0
release, on the released Xake 5.0.0.

## Where things stand

- `XakeVersion` `[5.0.0, 6.0)` (`bump-base -d XAKE_FLOOR=5.0.0`, BOM kept); the description and
  its fixture copy follow. `bump-base` moved the floor but not the locks (as before);
  `update-locks -d LOCK_REFRESH=Xake -d NUGET_FETCH=on` put Xake 5.0.0 in both locks. This machine
  has SDK 10.0.404, not 10.0.403: the `Compiler` lines were put back to 10.0.403 by hand and
  `xake.fingerprints` recomputed.
- `build.fsx` on `Xake 5.0.0` (its one FS0044, `getTargetFullName`, now `getTargetFile` +
  `File.getSysFileName`); `build.fsc.fsx` on `Xake 5.0.0` + `Xake.Dotnet 5.0.0-preview.1` (the
  newest 5.x on nuget.org today); `testjobs.fsx` has no `#r` and compiles under both; no FS0041
  or `File option` changes were needed in the scripts. Samples pin `Xake 5.0.0` (both run).
- The `docs/features/*/windows/fixture/accept.fsx` files (legacy-recipe, netfx-refasm,
  source-order) are historical: they pin the feature previews of 4.4.0 (`4.4.0-legacy.1`, ...)
  they checked on the Windows VM. Left unchanged.
- The root `.xake` / `.xake.fsc` were upgraded to database 0.7 (Build facts in `docs/session.md`).
- CI `xake-latest` (`XAKE_VERSION=5.*-*`) still makes sense: the newest 5.x, previews included.
- Getting-started `#r` lines and `Xake` ranges in the docs and package readmes are on 5.0.0.
- Verified locally: `dotnet fsi build.fsx -- -- build test pack` green (dotnet 172/172,
  hermetic 368/368, 0 warnings after the FS0044 fix); `pack -d VERSION=5.0.0` gives a nuspec with
  `Xake [5.0.0, 6.0.0)` and both assemblies for net462 and netstandard2.0;
  `build.fsc.fsx check` green. The fsc self-build (`build.fsc.fsx build`) needs SDK 10.0.403:
  left to CI `build-fsc`.
- Release notes: `5.0.0 (2026-11-21)` (date pending the owner: today or the merge day).

## Next step

Owner: confirm the release date and give the word to push. Then push `feature/scripts-xake5`,
open the PR to `dev` (squash), watch CI (`build-fsc` is the fsc self-build). After the merge the
owner tags `v5.0.0` on the squash commit; `master` follows by `--ff-only`; then `build.fsc.fsx`
moves to `Xake.Dotnet 5.0.0`.
