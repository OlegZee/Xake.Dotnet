# Tracker: editorconfig-paths

One line per item. Status as of 2026-11-19: implemented, local commit, all tests green.

## Open

- [ ] Owner: review and squash-merge PR #19 (`feature/editorconfig-paths` -> `dev`); release number
- [ ] Optional: an end-to-end record on the Windows VM (`win11`), if the owner wants one

## Done

- [x] Brief copied (`$(Root)`); SDK probe on 8.0.427, 11.0.100 (macOS) and 10.0.404 (Windows VM): values verbatim
- [x] Owner approved `design.md` (2026-11-19): no unescape, the doubling is JSON's
- [x] Docs: `docs/hermetic/lock.md` (analyzer config values, host strings in JSON), release notes "Unreleased" (Fixes)
- [x] `Import.fs`: `analyzerConfigPaths` in `entryOf` (`RecordContext.Roots` new); `hostStrings` on JSON-unescaped lines
- [x] Tests (`RecordTests.fs`, fixture `Recorded lock identities`): drive and UNC roots, untouched values/files, tokenized and expanded form; host strings in JSON
- [x] `dotnet fsi build.fsx -- -- build test pack`: all green (SDK 11.0.100 default), 93 lines added
