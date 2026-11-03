# Tracker: import-file-casing

One line per item. Status as of 2026-11-04.

## Done

- [x] Research and `brief.md` (2026-11-04)
- [x] Owner's answers 1-8 (2026-11-04, brief §7)

## Open

- [ ] `Casing.ofRepository`: the resolver (the git index read with `git ls-files`, cached per import; a path not in the index unchanged), with its unit tests. Next
- [ ] `describe`: project path, F# sources, the literal part of C# masks and excludes, resource files (msbuild's names kept), `keyFile`, project refs, packed icon/readme
- [ ] `record`: compiler arguments (`absolutize`), `Resources`, `Imports`/`Inputs`, `appOf` content and own config, `projectsOf` names, the project files the script names
- [ ] Legacy `Project.import` route (decision 6)
- [ ] Import warning for every difference (decision 3)
- [ ] `ResXFileRef` files: resolved at import, recorded in the lock (resx spelling -> repository path, hashed), the resx compile reads the recorded path (decision 7)
- [ ] `<include file=...>` in C# doc comments: a warning per miscased reference with both spellings (decision 8)
- [ ] Fixture `fixtures/casing/` and the import test (macOS, Windows leg; ignored on Linux)
- [ ] Docs: the hermetic import doc and the release notes (one re-import of miscased projects)
- [ ] Consumer re-import on macOS or Windows, then the build on Linux (the owner's)
