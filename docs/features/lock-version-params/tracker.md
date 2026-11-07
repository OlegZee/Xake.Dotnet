# Tracker: lock-version-params

One line per item. Status as of 2026-11-07.

## Open

- [ ] A consumer solution recorded with the option (two brands, 93 projects): one diff per
      entry, a second import changes nothing, builds with two suffix groups (the owner runs it
      with the preview package; steps and expectations in `consumer-check.md`)
- [ ] PR `feature/lock-version-params` -> `dev` (draft, after the owner's OK); release (4.2.0,
      together with or after `feature/import-file-casing`, which also changes `Import.fs`)
- [ ] Later, if asked for: a `Pack` helper for inter-package dependency versions / NU5104
      (decided 2026-11-07: docs only for now, `pack.md` and `versioning.md`)

## Done

- [x] Probe: SDK 8.0.427 with `VersionSuffix=<marker>` (AssemblyInfo, `deps.json`, `VERSION` env)
- [x] `RecordOptions.VersionSuffix`, `Import.versionMarker`, the marker in `recordGlobals`
- [x] `versionTokenize` over the entry's texts, `Properties.VersionPrefix`; the marker left
      anywhere else fails naming the file; a project with its own `Version`: literal + `Info`
- [x] `depsVersionTokens`: `$(Version:<file>)` in a recorded `deps.json`; `Apps.depsResolved`
- [x] `Locks.Settings.VersionSuffixes`, `versionIn` (Versions, then prefix + suffix), `Locks.versionOf`
- [x] `VERSION` env emptied for a marker import
- [x] SBOM root version from the built assembly when tokenized
- [x] Tests: `Version tokens` (pure), `Import record version token` (record twice identical,
      two groups and two frameworks, deps.json, rebuild per group, empty suffix, no mask, stale
      prefix, a stray marker)
- [x] Docs: `lock.md`, release notes (Unreleased)
- [x] Docs: `docs/hermetic/versioning.md` (how-to), links from `lock.md`, `index.md`,
      `workflows.md`, `pack.md`, `hermetic-guide.md` (commit `1981406`)
- [x] The branch moved to the worktree `lock-version-params-xake-8fa981` (2026-11-07); the old
      worktree `.claude/worktrees/feature/lock-version-params` removed
- [x] The suffix is validated (`Locks.suffixProblem`, in `versionIn`): not a SemVer 2.0
      pre-release label, `+` or a leading `-` fails the build naming the project and the mask;
      `versioning.md` ("A variant tag", "A valid suffix", messages), release notes
- [x] Tests: a new `CORE_SUFFIX` rebuilds core and app and updates `app.deps.json`; `Versions`
      wins over `VersionSuffixes` on a recorded lock; a bad suffix fails; `suffixProblem` (pure);
      `VersionSuffixes` with a `$(SiblingRoot)` mask; "records no VersionPrefix"
- [x] Acceptance 9: `Import record WPF`, the BAML and `Recorded.Markup` equal with and without
      the option (`lib` from `VersionPrefix`, `VERSION` emptied); `RecordKit.buildWith`
- [x] Found: the masks are script code, not a dependency (moving a group to `Versions` rebuilds
      nothing); documented in `versioning.md`, "What rebuilds"
- [x] `brief.md`, "Lock format changes": `$(Version:<file>)` in `deps.json`
- [x] Local preview package `out/pkg/Xake.Dotnet.4.2.0-version.1.nupkg` (not published)
- [x] Full `src/hermetic.tests`: 334 passed (2026-11-07)
