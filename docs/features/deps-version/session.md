# Session: deps-version

**Released in 4.4.0 (2026-11-19).** Only the "Later" items of `tracker.md` are open.

Branch `feature/deps-version`, cut from `dev` at `b4853da` (4.3.0). Task B-3 of the consumer
analysis: the project version in an application's `deps.json` from the lock (`brief.md`).

## Where things stand (2026-11-19)

- Design approved by the owner (`brief.md`, Decision); implemented as `design.md` says, plus its
  "Found while implementing" notes. Sizes: `Locks.fs` +37/-2, `Apps.fs` +17/-10; tests +65; docs +40.
- `origin/dev` (`d727ce3`: SDK 11 fix #17, lock-inputs #16) merged in with a merge commit;
  release notes keep both "Unreleased" entries. `build test pack` under SDK 11.0.100: all green.
- Pushed, PR into `dev` open (squash), not merged. `Import.fs` untouched.

## Next step

The owner reviews and squash-merges the PR; `build-fsc` in CI checks SDK 10.0.403. After the
merge: the consumer drops its `AssemblyInformationalVersion` overlay and re-imports (same
`deps.json`).
