# Session: xake5

Branch `feature/xake5`, cut from `origin/dev` `61b3b07` (= release 4.4.0). Started 2026-11-19.

## Where things stand

- Design approved by the owner (bootstrap option B: both scripts stay on `Xake 4.0.0`).
- Port implemented and committed locally, not pushed: `Out`/`Doc` as `File option`, Sign's
  delegated result, Pack's `recipe`, annotated recursive recipes, the deprecated aliases and
  `getRuleMatch` (tests, doc examples) replaced; `XakeVersion` `[5.0.0-preview.2, 6.0)`; CI
  `xake-latest` on `5.*-*`; locks on Xake 5.0.0-preview.2 with the SDK 10.0.403 compiler lines
  kept, `xake.fingerprints` recomputed, the fixture description copy updated.
- `dotnet fsi build.fsx -- -- build test pack` green (dotnet 172, hermetic 368), 0 warnings
  from the port; `build.fsc.fsx check` green. The fsc self-build (`build`) needs SDK 10.0.403:
  CI's `build-fsc` covers it.
- Windows VM not run: tool paths are `File.getSysFileName`, which is what `FullName` returned.

## Next step

Report to the owner; on the owner's word in this session push `feature/xake5` and open the PR to
`dev` (squash). After merge the owner tags `v5.0.0-preview.1`; then the follow-up moves both
scripts' `#r` lines (tracker "Open").
