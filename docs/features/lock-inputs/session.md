# Session: lock-inputs

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

## Next step

Install SDK 10.0.403 (needs the owner's go-ahead), then
`dotnet fsi build.fsc.fsx -- -- build check -d NUGET_FETCH=on`; rerun `build test pack` where the
SDK set matches CI. Then the owner's review, push and PR to `dev`.
