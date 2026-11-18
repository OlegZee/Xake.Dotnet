# Tracker: lock-inputs

One line per item. Status as of 2026-11-19: implemented, local commit; fsc self-build check pending (SDK 10.0.403 not installed).

## Open

- [ ] fsc self-build `build.fsc.fsx build check -d NUGET_FETCH=on` on SDK 10.0.403 (not installed on this machine: 8.0.427 and 11.0.100 only)
- [ ] Owner: review; push and PR `feature/lock-inputs` -> `dev` (squash); release number

## Later

- Post-compile outputs (a file a tool makes from the built dll, reading the attributes of the
  main sources): the consumer's build step. The same declaration could serve it: an add-on
  declares the `.cs` files whose attributes the tool reads as `XakeImportInput` (or a second item
  type, if the owner wants them apart), the lock records them in `Evaluation.Imports`, and the
  consumer's step `need`s the lock entry's list (a small `Locks` helper returning the declared
  inputs of an entry) so its own rule reruns when they change. Needs a way to tell declared
  inputs from msbuild files in the list (metadata or a separate key) only if that step must see
  them alone.

## Done

- [x] Owner approved the design and the three decisions as proposed (2026-11-19)
- [x] Docs: `lock.md` (declared inputs, hashing by kind, `Imports` field), `workflows.md` "Generated resources and their inputs", release notes "Unreleased"
- [x] `FileHash.isBinary`/`inputSha256`; `Lock.rehash` on it
- [x] `Import.record`: `XakeImport=true`; `XakeImportInput` dumped; `declaredInputs` into `Evaluation.Imports`, every input hashed by kind
- [x] Tests: `Import record declared inputs` (record by kind, package and generated files left out, CRLF-only current, image byte stale at build/check/warn, re-import current; missing file fails), `Line-end neutral hashes` (kind)
- [x] `build test pack`: all green but 7 `hermetic/rest` tests that fail on clean `dev` b4853da on this machine too (SDK 11.0.100 default, no SDK 10.0.x)

- [x] Analysis: the copy of binaries, `markupSources`, no `XakeImport` property today (`XakeRecordKey` only), the check path; design and line budget in `brief.md` (2026-11-19)
