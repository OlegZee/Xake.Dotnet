# Tracker: editorconfig-paths

One line per item. Status as of 2026-11-19: design reported, waiting for the owner.

## Open

- [ ] Owner: approve `design.md` (the brief's even-run unescape replaced: the doubling is JSON's)
- [ ] Docs: `docs/hermetic/lock.md`, release notes "Unreleased"
- [ ] `Import.fs`: `analyzerConfigPaths` in `entryOf`; `hostStrings` on JSON-unescaped lines
- [ ] Tests in `src/hermetic.tests`
- [ ] `dotnet fsi build.fsx -- -- build test pack`
- [ ] Owner: review; push and PR `feature/editorconfig-paths` -> `dev` (squash)

## Done

- [x] Brief copied (`$(Root)`); SDK probe on 8.0.427, 11.0.100 (macOS) and 10.0.404 (Windows VM): values verbatim
