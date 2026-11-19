# Tracker: source-order

One line per item. Status as of 2026-11-19: design proposed.

## Open

- [ ] Owner approves the design (`brief.md`); confirm the `Options` cause against one of the consumer's 4 test-project lock pairs
- [ ] Merge `origin/dev` with B-4 (`feature/legacy-recipe`) before coding; ask the orchestrator if B-4 is not merged
- [ ] `Lock.portableSources`, `Lock.portableItemSwitches`, `canonicalSourcesBy`; `Import.entryOfWith`
- [ ] Tests: pure (two orders, generated, explicit), `entryOfWith` in two orders, Options blocks
- [ ] `lock.md` (the rule and the Windows trade-off), release notes "Unreleased"
- [ ] `dotnet fsi build.fsx -- -- build test pack`
- [ ] Windows VM: glob-heavy project, Windows lock equals the macOS one

## Done

- [x] Cause found: msbuild's `Array.Sort(OrdinalIgnoreCase)` per glob on the OS's path spelling; `\` vs `/` (probes `ord1`, `t2`, 2026-11-19)
