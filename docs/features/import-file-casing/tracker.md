# Tracker: import-file-casing

One line per item. Status as of 2026-11-04.

## Done

- [x] Research and `brief.md` (2026-11-04)
- [x] Owner's answers 1-8 (2026-11-04, brief §7)
- [x] `Casing`: the resolver (`Casing.read` runs `git ls-files -z --full-name` once per import; `Casing.resolve`, `Casing.Resolver`; a path not in the index or outside the root unchanged; two entries by case: exact wins, else fails), with its unit tests (`CasingTests.fs`)
- [x] `Csc/Fsc.ResxFiles` (`ResxFile`: resx, the resx's spelling, the repository's file, its hash), the lock's optional `ResxFiles` (format stays 2; an old lock reads as none), the resx compile reads the recorded file (`ResxFormat.readWith`, the runner's `ResxFiles`)
- [x] `describe`: project path, F# sources, the literal part of C# masks and excludes (`repositoryMask`), resource files (msbuild's names kept, written as `name` where the repository's spelling derives another), `keyFile`, project refs, packed icon/readme
- [x] `record`: the project files the script names (resolved before msbuild runs), compiler arguments (`Casing.argument`: an unnamed `/resource:` keeps its file name as its name), `Resources`, `Imports`/`Inputs`, `appOf` content and own config, app sources, `projectsOf` names (and the referenced output's file name)
- [x] Legacy `Project.import` route (decision 6): `parseImportWith` (arguments, resx, imports, project refs, the project), the project files; no `ResxFiles` in its format-1 lock
- [x] Import warning for every difference (decision 3): `Imported.Casing`, `  casing (<project>): '<project's>' is '<repository's>' in the repository (git index): recorded so`, Info, once per import
- [x] `ResXFileRef` files resolved at import and recorded in the lock (decision 7), with an Info line per miscased one
- [x] `<include file=...>` in C# doc comments: an Info line per miscased one with both spellings (decision 8)
- [x] Fixture import test (`Import casing`: an fsproj and a csproj, run where the folder ignores case, ignored elsewhere)

## Open

- [ ] Docs: the hermetic import doc and the release notes (one re-import of miscased projects)
- [ ] Consumer re-import on macOS or Windows, then the build on Linux (the owner's)
