# Tracker: deps-version

One line per item. Status as of 2026-11-19: design proposed, waiting for the owner.

## Open

- [ ] Owner approves `design.md` (the `Locks.Settings.LockOf` field, fallback, error, line budget)
- [ ] Docs: `lock.md` (Settings, applications), `versioning.md`, `hermetic/README.md`, release notes "unreleased"
- [ ] Code: `Locks.Settings.LockOf`, the version map in `compileAssembly`, `Apps.depsResolved`/`write`
- [ ] Tests: `AppTests` (pure), `RecordTests` version token (`GenerateAssemblyInfo=false` project)
- [ ] `dotnet fsi build.fsx -- -- build test pack`; compare failures with clean dev
- [ ] Report to the owner; push and PR only on their word

## Done

- [x] Brief copied (`brief.md`); code read at `b4853da`; design written (`design.md`)
