# Tracker: deps-version

One line per item. Status as of 2026-11-19: PR open into `dev`, not merged.

## Open

- [ ] Owner reviews and squash-merges the PR (pushed and opened 2026-11-19 on their word)
- [ ] CI green, `build-fsc` (SDK 10.0.403) included
- [ ] Consumer side: drop the overlay that injects `AssemblyInformationalVersion`, re-import, same `deps.json`

## Done

- [x] Brief copied (`brief.md`), design (`design.md`) approved by the owner 2026-11-19
- [x] Docs: `lock.md` (Settings, version token), `versioning.md`, release notes "Unreleased"
- [x] Code: `Locks.Settings.LockOf`, the version map in `compileAssembly`, `Apps.depsResolved`/`write`
- [x] Tests: `AppTests` (pure: lock, fallback, error), `RecordTests` (`GenerateAssemblyInfo=false` project, suffix change, no lock)
- [x] `build test pack`: no new failures against clean dev (the same 6 fail on both, SDK 10 missing locally)
