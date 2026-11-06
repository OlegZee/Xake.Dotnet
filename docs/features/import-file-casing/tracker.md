# Tracker: import-file-casing

One line per item. Status as of 2026-11-06.

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
- [x] Docs: `docs/hermetic/description.md` "The repository's spelling of a file", `lock.md` (`ResxFiles`), the release notes ("Unreleased": one re-import of miscased projects)
- [x] Owner's answers 9-12 (2026-11-05, brief §7)
- [x] Answer 9: an application's recorded `deps.json` names a miscased project reference's runtime file as the build writes it (`Import.depsFileNames`; library and assembly names kept); fixture test (exe app, run folder, the app runs) and a pure test
- [x] Answers 10-12: release notes as `4.1.0 (unreleased)` (the `Csc/Fsc.ResxFiles` record change, the one re-import, resx-referenced files rebuild the resources); `ResxFiles` hashes recorded, not checked; every `ResXFileRef` file recorded (as implemented)
- [x] Merge of `origin/dev` (WPF record, 4.1.0 released): conflicts resolved keeping both sides; the self-build's lock and fingerprints regenerated; release notes `4.2.0 (unreleased)` above 4.1.0
- [x] WPF: the markup inputs (`Page`, `ApplicationDefinition`, `Resource`) in `Evaluation.Imports` and the markup run's referenced outputs in the repository's spelling; BAML keys, `Recorded.Markup` identities and `g.resources` bytes stay msbuild's; fixture test (a miscased Page and Resource)
- [x] Each path in the spelling of the repository that contains it (`Casing.Repos`: per folder `rev-parse --show-cdup`, one `ls-files` per repository; submodules their own repositories; package folder and .NET installation excluded; one Info line per folder tree in no repository); fixture: a project in a submodule of a sibling repository behind a declared root
- [x] Flake root cause: the IDE (VS Code C# Dev Kit) builds the test fixtures of worktrees under its workspace while the tests run (see session)
- [x] Consumer rerun on Linux (903129b, the coordinator): 13 paths corrected; two gaps fixed: `ResxFiles` recorded under any root of the lock (not only the project root; the log says what happens), doc `<include>` looked up as csc does (the source's folder, then the project's); fixture extended (a resx image and a project-folder include in the sibling's submodule, built from the lock)

## Open

- [ ] Consumer re-import on macOS or Windows, then the build on Linux (the owner's)
