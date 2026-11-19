# Tracker: source-order

One line per item. Status as of 2026-11-19: implemented, not pushed.

## Open

- [ ] Owner's go to push; PR `feature/source-order` -> `dev` (squash)
- [ ] Confirm the `Options` cause against one of the consumer's 4 test-project lock pairs (the consumer's re-import with the next preview)

## Later (out of scope)

- [ ] Test projects on Windows: testhost x64 files in `App/Content`, `win-x64`/`win-x86` targets in `deps.json`

## Done

- [x] Windows VM (`windows/`): the glob fixture's Windows lock equals the macOS one (SDK patch normalised); control with 4.4.0-preview.1 shows the difference (2026-11-19)
- [x] `dotnet fsi build.fsx -- -- build test pack` green; this repository's locks unchanged (F#, recorded on macOS: identity) (2026-11-19)
- [x] `Lock.canonicalSourcesBy`, `Lock.portableSources`, `Lock.portableItemSwitches`; `Import.entryOfWith`; tests (pure two orders, entry in two orders); `lock.md`, release notes (2026-11-19)
- [x] Owner approved the design; `origin/dev` with B-4 (`2fd8dc5`) merged in (2026-11-19)
- [x] Cause found: msbuild's `Array.Sort(OrdinalIgnoreCase)` per glob on the OS's path spelling; `\` vs `/` (probes `ord1`, `t2`, 2026-11-19)
