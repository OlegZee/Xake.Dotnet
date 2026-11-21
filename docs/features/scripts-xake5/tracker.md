# Tracker: scripts-xake5

One line per item. Status as of 2026-11-21.

## Open

- [ ] Owner: the release date in `docs/release-notes.md` (2026-11-21 written; or the merge day)
- [ ] Push and PR `feature/scripts-xake5` -> `dev` (squash) on the owner's word in this session
- [ ] CI `build-fsc` confirms the fsc self-build on SDK 10.0.403 (not installed here)
- [ ] After the merge: the owner tags `v5.0.0` on the squash commit; `master` follows by `--ff-only`
- [ ] After the publish: `build.fsc.fsx` on `#r "nuget: Xake.Dotnet, 5.0.0"`

## Later (out of scope, proposals for the owner)

- [ ] `bump-base` does not re-resolve Xake in the locks: its `LOCK_REFRESH` environment variable does not reach the lock rule (`Var.read` of the build variable); `update-locks -d LOCK_REFRESH=Xake` does
- [ ] `bump-base`'s `XakeFloor.newest` and its messages still speak of "4.x" (works for 5.x: it takes every major >= 4)

## Done

- [x] `XakeVersion` `[5.0.0, 6.0)` by `bump-base -d XAKE_FLOOR=5.0.0` (BOM kept); description and its test fixture copy follow (2026-11-21)
- [x] Locks on Xake 5.0.0 (`update-locks -d LOCK_REFRESH=Xake -d NUGET_FETCH=on`), the SDK 10.0.403 `Compiler` lines put back, `xake.fingerprints` recomputed (2026-11-21)
- [x] `build.fsx` on `Xake 5.0.0`; `build.fsc.fsx` on `Xake 5.0.0` + `Xake.Dotnet 5.0.0-preview.1`; samples on `Xake 5.0.0` (2026-11-21)
- [x] Getting-started `#r` lines and ranges to 5.0.0: readme, hermetic-guide, tasks, hermetic lock/workflows/versioning, src readmes (2026-11-21)
- [x] Release notes `5.0.0`; `docs/session.md` Release state and Build facts (2026-11-21)
