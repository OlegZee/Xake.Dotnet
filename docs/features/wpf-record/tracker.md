# Tracker: wpf-record

One line per item. Status as of 2026-11-05.

## Open

- [ ] Fixture: a small WPF library whose XAML root is its own type (pass 2), one referenced project, `net8.0-windows`
- [ ] Markup run in `Import.record`: real referenced outputs in the run's `OutDir` (from `OutOf`), no global `SkipCompilerExecution`; remove the up-front refusal
- [ ] Missing referenced output: the import fails naming the file and the project
- [ ] `Page`/`ApplicationDefinition`/`Resource` items into `Evaluation.Imports` (text hash); build and `check` say "run the import again"
- [ ] Tests: record + build from the lock; `.g.resources` byte-equal to msbuild's; replace the refusal test in `RecordTests.fs`
- [ ] `net472` markup (probe on macOS, then a test leg if it runs)
- [ ] Decide: referenced identities pinned by the BAML (brief, "Open decisions")
- [ ] Docs: README refusal line, `lock.md` example, release notes line

## Done

- [x] Analysis and probes (brief, "Evidence"); feature folder (2026-11-05)
