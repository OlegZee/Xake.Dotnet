# Session: editorconfig-paths (B-1)

Branch `feature/editorconfig-paths`, cut from `dev` `d727ce3`. Only `src/hermetic/Import.fs`
changes; B-5 and B-4 (also `Import.fs`) wait for this one. Do not touch PR #12.

## Where things stand (2026-11-19)

- Implemented as `design.md` (approved): `Import.analyzerConfigPaths` writes a Windows-spelled
  value under a root with `/` in recorded `.editorconfig`/`.globalconfig` texts (then tokenized
  as usual); `Import.hostStrings` also reads each line with JSON's `\\` undone.
- `GenerateMSBuildEditorConfig` writes values verbatim (probed on 8.0.427, 11.0.100 macOS,
  10.0.404 Windows VM); the doubled backslashes of the brief were the lock's JSON escaping.
- `build test pack` green; local commits only, nothing pushed.

## Next step

Owner review; on their word push `feature/editorconfig-paths` and open the PR to `dev` (squash).
Then B-5, B-4 on `Import.fs`.
