# Session: lock-inputs

**Released in 4.4.0 (2026-11-19).** Only the "Later" items of `tracker.md` are open.

Branch `feature/lock-inputs` from `dev` at `b4853da` (4.3.0). Goal and design: `brief.md`;
items: `tracker.md`. PR #12 (`feature/import-file-casing`, open) also touches `Import.fs`: keep
this diff to `entryOf`, `designTimeItems` and `record`'s `extensions`.

## State at 2026-11-19

- Design reported to the orchestrator; no code yet. Findings: there is no `XakeImport` property
  (the import sets `XakeRecordKey` only); `Evaluation.Imports` + `Locks.staleOf` are already the
  common list and the check path; `FileHash.textMatches` already accepts raw hashes, so binary
  inputs need no check change.

- Implemented (approved design, `brief.md`). Code +37 lines (`Import.fs` 18, `Lock.fs` 15, `Locks.fs`
  doc 4), tests +82 (`RecordTests.fs` 64, `LineEndTests.fs` 18), docs +68. New tests green.
- `build test pack`: 7 failures in `hermetic/rest` (fsproj import, resx bytes, toolset, external
  satellites); the same tests fail on clean `dev` here: the machine's default SDK is now 11.0.100
  (sourcelink path quoting, `net11.0`) and no SDK 10.0.x is installed. Not this change.

- fsc self-build: SDK 10.0.403 is not installed here (10.0.404 is; the locks name the compiler
  by exact path). In a throwaway worktree with `global.json` pinned to 10.0.404, `import
  update-locks` then `build check` passed; the lock diff was only the compiler path/version and
  the `dotnet-sdk` prerequisite from that pin, no `Imports` hash changed. So the committed locks
  need no update for this change.
- Seen on the way (not this feature): `import update-locks` in a folder whose path has `--`
  rewrote it to `__` and failed ("Could not find a part of the path").

## Next step

The owner's review; on their word, push `feature/lock-inputs` and open the PR to `dev` (squash),
where CI's `build-fsc` runs the self-build on SDK 10.0.403.
