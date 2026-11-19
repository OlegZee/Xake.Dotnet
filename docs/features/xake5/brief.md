# Brief: xake5 — Xake.Dotnet 5.0.0 on the Xake 5 engine

Status: design, waiting for the owner's approval (2026-11-19). No code yet.

## Goal

Port `Xake.Dotnet` (both assemblies) to `Xake 5.0.0-preview.2` and release it as
`Xake.Dotnet 5.0.0` (first `5.0.0-preview.1`, the owner's tag). A port: no new features; what
Xake 5 offers beyond goes to the tracker's "Later".

## What breaks here (verified by a trial compile against 5.0.0-preview.2)

The probe: a copy of `61b3b07`, `-p:XakeVersion=5.0.0-preview.2`, every project built.

Errors (the compiler):

| Xake 5 change | Place | Fix |
|---|---|---|
| `File.undefined` removed | `Csc.fs` 117, 589, 673, 752; `Fsc.fs` 61, 69, 315, 421, 423, 486, 509; `Described.fs` 670 (`plan`'s `out`) | `CscSettings.Out`, `FscSettings.Out`, `FscSettings.Doc`: `File option`; `plan`'s `out: File option` |
| `FileDep of File` (no stamp), `ArtifactDep`/`ArtifactDep2` removed | `Sign.fs` 389 (the delegated executor's result); tests `dotnet.tests/DescriptionTests.fs` 136, 161; `hermetic.tests/DescriptionTests.fs` 106-108 | `FileDep (File.make input)`; tests match `FileDep f` / `TargetDep (FileTarget f)` |
| `BuildResult` gains `Epoch`, `StaticCount` | `Sign.fs` 387 | `Epoch = 0; StaticCount = 0` (the engine stamps the epoch of a delegated result, `ExecCore.runDelegated`) |
| One `FileRule (outputs, header, recipe)` | `hermetic.tests/SignTests.fs` 124, 134 | `FileRule ([mask], _, _)` |
| `recipe` is a `RuleRecipeBuilder` value in `Xake.RuleRecipe` (no `Xake.Builder.recipe`) | `Pack.fs` 48 (`Options.current`) | `Xake.RuleRecipe.recipe` |
| `RecipeBuilder.Bind`/`Delay` gain overloads (`Headed`, `RuleHeader`): a recursive recipe function without its type does not infer | `Described.fs` 409 `walk`, 476 `gather`, 483 `each`; `hermetic.tests/RecordTests.fs` 2118 (a fold's accumulator) | annotate the return type `Recipe<ExecContext, ...>` |

Warnings (FS0044, deprecated aliases): `FullName`, `File.getFullName`, `getTargetFullName` —
about 60 sites in `src/dotnet` (Csc 11, Fsc 14, DotnetTasks 7, Description 6, Satellite 4,
Dotnet.satellite 4, ProjectPlan 3, Dotnet.resgen 1), 22 in `src/hermetic` (Locks 18, Described 5,
Import 4, Sign 2) and about 105 in the tests. Not errors; the aliases go in a later Xake.

Not touched by this repository (verified by search): `MultiFileRule`, `FileConditionRule`,
`Storage`/`openDb`/`resultPU`, `getRuleMatch`, `Typed.Matched`, `requiring`, statements with a value
inside `recipe { }` (no FS0020 left), `ExecContext` construction.

## Decisions proposed

1. **`Out`/`Doc` as `File option`.** The builders' `out`/`doc` operations keep taking a `File` and
   set `Some`: a script with `csc { out (File.make "a.dll") }` does not change; a script that
   writes the record (`{ CscSettings.Default with Out = File.make .. }`) writes `Some (..)`.
   `None` keeps today's meaning (the rule's target, `getTargetFile`).
2. **The deprecated aliases.** Replaced in this port, per site: a path handed to a tool or the OS
   (command lines, `System.IO`) -> `File.getSysFileName f`; a path compared with or written as
   Xake's name -> `f.Name`. Where unsure, `getSysFileName` (it is what `FullName` returns
   today, so behaviour does not move). `getTargetFullName ()` -> `getTargetFile ()` +
   `getSysFileName`. Warnings left: none.
3. **Version.** `XakeVersion` `[5.0.0-preview.2, 6.0)` now, `[5.0.0, 6.0)` at the release;
   `Xake.Dotnet` 5.0.0, first tag `v5.0.0-preview.1` (owner).
4. **Bootstrap.** `build.fsx` runs on `Xake 5.0.0-preview.2` unchanged (checked: one FS0044 at
   line 135, `getTargetFullName`). `build.fsc.fsx` loads a published `Xake.Dotnet`; one built for
   Xake 4 cannot run on Xake 5 (`File.undefined` is gone), so it stays on `Xake 4.0.0` +
   `Xake.Dotnet 4.0.0-preview.1` until `Xake.Dotnet 5.0.0-preview.1` is on nuget.org, then both
   lines move (a small follow-up PR). Both scripts use the database `.xake` at the repository
   root: Xake 4 cannot read 0.7 and recreates it, so locally alternating the two scripts rebuilds
   everything once each way in between (CI runs each in a fresh checkout). Proposed: option A,
   `build.fsx` moves now (it tests the port on the engine it targets); option B, both stay on 4.0.0
   and move together after the preview publish.
5. **Locks.** `locks/*/Release.json` record the package `Xake` 4.0.0: the hermetic test "the
   repository's projects build from their generated locks" fails until they are re-recorded
   (`build.fsc.fsx -- -- import update-locks -d NUGET_FETCH=on`, with Xake 5 as the compile
   input) together with `xake.fingerprints` and the hermetic fixture copy. `update-locks` only
   records (msbuild resolves `XakeVersion` from the project file), so it should run on the old
   bootstrap (decision 4) — not yet tried.

## Line budget

- Compile errors: ~25 lines (src 12, tests 13). `Out`/`Doc` as `File option`: ~15 in src, 16
  record writers in tests.
- Deprecated aliases: ~80 lines in src, ~105 in tests, one-for-one.
- Version/bootstrap/CI: ~5 lines (`XakeVersion`, `build.fsx` `#r`, `build.yml` `xake-latest`
  `5.*-*` and its comment). Test log assertion: 1-2.
- Docs: release notes "5.0.0 (unreleased)" with the migration section ~50; `csc-syntax.md`,
  `hermetic/{lock,versioning,pack,workflows}.md`, `docs/session.md` ~15.
- Generated: `locks/*/Release.json`, `xake.fingerprints`, the fixture copy (a few lines each).
- About 300 changed lines of code, most mechanical; ~65 of docs.

## Behaviour on Xake 5 (the probe's tests)

- `dotnet.tests`: 172/172 pass.
- `hermetic.tests`: 366/368. The two failures: (a) a test expects `Started <absolute path>` in
  the log; Xake 5 logs targets relative to the project root (`Started out/Release/.../Cs.dll`) —
  the assertion changes; (b) the repository's locks still name Xake 4.0.0 (decision 5).
- No other behaviour change surfaced: lock contents of the fixtures are unchanged (the
  hermetic code writes its own `/`-paths, not `File.Name`).
