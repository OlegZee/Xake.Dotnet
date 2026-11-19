# Tracker: netfx-refasm

One line per item. Status as of 2026-11-19: probed, design reported, waiting for the owner.

## Open

- [x] Probe the SDK targets (8.0.427, 11.0.100) and the Windows VM (8.0.428, 9.0.320, 10.0.404; 4.7.2 pack installed)
- [ ] Owner: approve the design, decide D1 (how a version change makes a lock stale)
- [ ] Docs: `lock.md` (next to "Packs: from NuGet"), `hermetic-mode.md` row, release notes "Unreleased"
- [ ] `Import.fs`: `NetFxReferenceAssemblies` type, two `RecordOptions` fields, `recordGlobals`, `restoreProps`
- [ ] Tests: CPM fixture, version change, unresolved GAC-only reference, non-netfx byte-identical, `recordGlobals`
- [ ] Windows acceptance on the VM (Package lock equals the macOS lock; Installed fails as today)
- [ ] `build test pack`; fsc self-build locks unchanged (no netfx project in this repository)
- [ ] Owner: review, push, PR to `dev` (squash)

## Later

- B-4 (legacy projects recipe) uses this as its section D.
