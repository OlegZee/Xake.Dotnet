# Session: lock-inputs

Branch `feature/lock-inputs` from `dev` at `b4853da` (4.3.0). Goal and design: `brief.md`;
items: `tracker.md`. PR #12 (`feature/import-file-casing`, open) also touches `Import.fs`: keep
this diff to `entryOf`, `designTimeItems` and `record`'s `extensions`.

## State at 2026-11-19

- Design reported to the orchestrator; no code yet. Findings: there is no `XakeImport` property
  (the import sets `XakeRecordKey` only); `Evaluation.Imports` + `Locks.staleOf` are already the
  common list and the check path; `FileHash.textMatches` already accepts raw hashes, so binary
  inputs need no check change.

## Next step

Wait for the owner's approval of `brief.md` (three decisions). Then docs first (`lock.md`,
`workflows.md`, release notes), then `Lock.fs`/`Import.fs`, then the tests.
