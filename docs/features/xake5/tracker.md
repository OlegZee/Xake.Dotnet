# Tracker: xake5

One line per item. Status as of 2026-11-19: design reported, waiting for the owner's approval.

## Open

- [ ] Owner approves the design (brief.md; the bootstrap choice and `Out`/`Doc` as `File option`)
- [ ] Docs first: release notes "5.0.0 (unreleased)" with a consumer migration section; `csc-syntax.md` (`out` default), docs naming `FullName`/`getTargetFullName`/`ArtifactDep2`
- [ ] `XakeVersion` `[5.0.0-preview.2, 6.0)`; CI `xake-latest` on `5.*-*`
- [ ] Errors: `File.undefined` -> `File option` (Csc, Fsc, Described.plan); Sign's `BuildResult`/`FileDep`; Pack's `recipe`; recursive recipes annotated
- [ ] Deprecated aliases replaced (`FullName`, `getFullName`, `getTargetFullName`): src ~80 sites, tests ~105
- [ ] Tests adapted (`FileDep f`, `TargetDep`, `FileRule ([mask], _, _)`, `Out = Some ..`)
- [ ] Bootstrap: `build.fsx` / `build.fsc.fsx` per the approved option
- [ ] `dotnet fsi build.fsx -- -- build test pack` green; locks unchanged (or re-recorded)
- [ ] Windows VM check of the hermetic tests if a path change touches Windows behaviour
- [ ] Report before push; push on the owner's word in this session

## Later (out of scope, proposals for the owner)

- [ ] csc/fsc rules: `also` for the `.pdb` / `.xml` side outputs (Xake 5 checks every output is written)
- [ ] Hermetic rules: header `needs` for the lock and description inputs (built before the body, content cut-off)
- [ ] `requires` instead of `withResource`/`CSC_JOBS` inside the compile recipes (WaitTime accounted by the engine)
- [ ] `build.fsx`: `let! m = matched` instead of `getRuleMatch`-style code, if any
- [ ] Remove `Xake.Dotnet`'s own `Path`-case assumptions now that filesets ignore case on Linux

## Done

- [x] Trial compile against 5.0.0-preview.2 (scratch copy): the full error list, ~25 changed lines to compile everything (2026-11-19)
- [x] `build.fsx` runs on `Xake 5.0.0-preview.2` unchanged (one FS0044 at line 135) (2026-11-19)
