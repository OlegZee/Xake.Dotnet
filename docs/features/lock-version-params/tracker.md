# Tracker: lock-version-params

One line per item. Status as of 2026-11-07.

## Open

- [ ] PR `feature/lock-version-params` -> `dev` (draft, after the owner's OK); release 4.2.0
      with this feature only, public, no preview (`release.md`); `feature/import-file-casing`
      (#12) stays out (2026-11-07: the owner is unsure the fix belongs in the import)
- [ ] Later, if asked for: `FileVersion` as a token too, for the scheme that puts the build
      number into its fourth part (`1.4.0.512`); it does not change an assembly's identity, unlike
      `AssemblyVersion`. Out of scope of 4.2.0 (owner, 2026-11-07): the consumer's CI keeps
      `FileVersion = <prefix>.0` and puts the build number in `Version` only, which 4.2.0
      reproduces; a number in `FileVersion` would be new behaviour (where the number comes
      from, the 65535 limit of a Win32 version part)
- [ ] Later, if asked for: a `Pack` helper for inter-package dependency versions / NU5104
      (decided 2026-11-07: docs only for now, `pack.md` and `versioning.md`)

## Done

- [x] Probe: SDK 8.0.427 with `VersionSuffix=<marker>` (AssemblyInfo, `deps.json`, `VERSION` env)
- [x] `RecordOptions.VersionSuffix`, `Import.versionMarker`, the marker in `recordGlobals`
      (later `RecordOptions.VersionToken: bool`, the marker internal)
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
- [x] Consumer check with `4.2.0-version.1` (2026-11-07; 93 projects, 186 locks, 9 prefix
      groups, SDK 8.0.427): first import changed only the version (plus an unrelated SDK pin
      8.0.426 -> 8.0.427), no marker left, no project with its own `Version`, WPF `g.resources`
      unchanged; second import and one with `VERSION` set: no change; builds with a
      `$(Root)/<folder>/**` group and `**`: 65 + 125 assemblies with their group's suffix, the
      same suffix rebuilt nothing, a new one all 190; no `$(` in 40 `deps.json`, each project
      with its own group's version; the `+` and no-mask failures as documented. Packages
      (`Pack.fromNuspec`, version from `Locks.versionOf`): 46 packages follow the suffix;
      dependencies on other groups' packages are literals in the consumer's nuspecs (the
      script's to move to `Locks.versionOf`, not this feature's)
- [x] The "no mask matches" and "not a SemVer pre-release label" messages show a mask with a
      root by its expansion (`$(Root)/x/** = /abs/x/**`) next to the project's absolute path
      (found by the consumer check); tested in `a version mask may start with a script root`
- [x] Checked: SDK 8.0.427's own `deps.json` with a suffix has the project's full version in a
      referenced project's `"assemblyVersion"` (`1.4.0-ci.1-b`, `fileVersion` `1.4.0.0`), as
      the recorded `$(Version:<file>)` resolves
- [x] `RecordOptions.VersionToken: bool` instead of `VersionSuffix: string option`; the marker
      internal, its format check gone (owner, 2026-11-07, before the release)
- [x] CI: `test-restore` before the parallel test builds (a restore race in `xake-latest`)
