# Tracker: import-file-casing

One line per item. Status as of 2026-11-04.

## Done

- [x] Research and `brief.md` (2026-11-04)

## Open

- [ ] Owner answers the brief's decisions 1-6. Next
- [ ] `Casing.ofRepository`: the resolver (git index + directory walk, cached per import), with its unit tests
- [ ] `describe`: project path, F# sources, the literal part of C# masks and excludes, resource files (msbuild's names kept), `keyFile`, project refs, packed icon/readme
- [ ] `record`: compiler arguments (`absolutize`), `Resources`, `Imports`/`Inputs`, `appOf` content and own config, `projectsOf` names, the project files the script names
- [ ] Legacy `Project.import` route (decision 6)
- [ ] Import warning per corrected file (decision 3)
- [ ] Replay: a missing recorded path with a case-only match fails naming both spellings (decision 4)
- [ ] Fixture `fixtures/casing/` and the import test (macOS, Windows leg; ignored on Linux)
- [ ] Docs: the hermetic import doc and the release notes (one re-import of miscased projects)
- [ ] Consumer re-import on macOS or Windows, then the build on Linux (the owner's)
