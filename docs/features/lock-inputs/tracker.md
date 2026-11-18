# Tracker: lock-inputs

One line per item. Status as of 2026-11-19: design reported, waiting for the owner.

## Open

- [ ] Owner: approve the design and the three decisions (`brief.md`, "Decisions for the owner")
- [ ] Docs: `lock.md` "Declared inputs", `workflows.md` how-to, release notes "Unreleased"
- [ ] `FileHash.inputSha256` (binary by a NUL in the first 8000 bytes), `Lock.rehash` on it
- [ ] `Import.record`: `XakeImport=true`; `XakeImportInput` dumped; `declaredInputs` into `Evaluation.Imports`
- [ ] Tests: the menu-compiler fixture (record, stale on a PNG byte, CRLF-only current, package file, missing file, WPF unchanged)
- [ ] `build test pack` green; fsc self-build `build check` on SDK 10.0.403

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

- [x] Analysis: the copy of binaries, `markupSources`, no `XakeImport` property today (`XakeRecordKey` only), the check path; design and line budget in `brief.md` (2026-11-19)
