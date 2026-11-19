# Tracker: netfx-refasm

One line per item. Status as of 2026-11-19: implemented, local commit; waiting for the owner's review.

## Done

- [x] Probe the SDK targets (8.0.427, 11.0.100) and the Windows VM (8.0.428, 9.0.320, 10.0.404; packs v4.0 to v4.8.1 installed)
- [x] Owner: design approved; D1 = (a), a changed version changes the locks on their next import (no new check); hiding the root, not an explicit reference
- [x] Docs: `lock.md` (after "Packs: from NuGet"), `hermetic-mode.md` row, release notes "Unreleased"
- [x] `Import.fs`: `NetFxReferenceAssemblies` type, two `RecordOptions` fields, `recordGlobals`, `restoreProps`
- [x] Tests: `NetFxReferenceTests.fs` (package + CPM + GAC-only reference + build, the configured version, other frameworks byte-identical), `recordGlobals` in `RecordTests.fs`
- [x] Windows acceptance on the VM (`windows/`): SDK 10.0.404 and 8.0.428 pass; locks byte-identical to macOS

## Open

- [x] `build test pack`: all green
- [x] fsc self-build: no update (its locks come from descriptions, not `Import.record`)
- [ ] Owner: review, push, PR to `dev` (squash)

## Later

- B-4 (legacy projects recipe) uses this as its section D.
- Seen on the way (not this feature): an import under a path with `--` rewrites it to `__` and fails (also seen in lock-inputs).
