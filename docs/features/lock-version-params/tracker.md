# Tracker: lock-version-params

One line per item. Status as of 2026-11-07.

## Open

- [ ] Acceptance 9 (WPF fixture identical with and without the option) has no test of its own;
      the BAML names `AssemblyVersion` only, which the marker does not reach
- [ ] A consumer solution recorded with the option (two brands, 93 projects): one diff per
      entry, a second import changes nothing, builds with two suffix groups
- [ ] PR `feature/lock-version-params` -> `dev`; release (4.2.0, together with or after
      `feature/import-file-casing`, which also changes `Import.fs`)

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
