# Session: xake5

Branch `feature/xake5`, cut from `origin/dev` `61b3b07` (= release 4.4.0). Started 2026-11-19.

## Where things stand

- Design reported to the orchestrator session (brief.md, tracker.md); no code changed.
- The trial compile (a scratch copy against `Xake 5.0.0-preview.2`) is in brief.md: every
  project compiles with about 25 changed lines; the deprecated aliases leave ~190 FS0044
  warnings.
- `build.fsx` runs on `Xake 5.0.0-preview.2` unchanged. `build.fsc.fsx` needs a published
  `Xake.Dotnet` built for Xake 5 (`5.0.0-preview.1`), so it cannot move in this PR.

## Trap

- Both scripts use the database `.xake` at the repository root. Xake 5 upgrades a 0.6 database
  to 0.7; Xake 4 cannot read 0.7 and recreates it (a full rebuild, with a warning). With
  `build.fsx` on Xake 5 and `build.fsc.fsx` on Xake 4, alternating the two locally rebuilds
  everything each time; CI jobs run in fresh checkouts and are not affected.

## Next step

Wait for the owner's approval of the design; then docs first (release notes migration section),
then the code in the order of tracker.md "Open".
