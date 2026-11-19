# Session: editorconfig-paths (B-1)

Branch `feature/editorconfig-paths`, cut from `dev` `d727ce3`. Only `src/hermetic/Import.fs`
changes; B-5 and B-4 (also `Import.fs`) wait for this one. Do not touch PR #12.

## Where things stand (2026-11-19)

- Design in `design.md`, reported to the orchestrator; no code yet.
- Finding: `GenerateMSBuildEditorConfig` writes values verbatim on every SDK probed; the doubled
  backslashes of the brief are the lock's JSON escaping. The fix is a root-anchored `\` -> `/`
  of analyzer-config values plus a JSON-aware `hostStrings`, not the brief's even-run unescape.

## Next step

Wait for the owner's approval of `design.md`; then docs, code, tests, `build test pack`, report
before push.
