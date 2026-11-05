# Tracker: wpf-record

One line per item. Status as of 2026-11-05.

## Open

- [ ] Decide: referenced identities pinned by the BAML (brief, "Open decisions"); today documented only
- [ ] Run a consumer's WPF output on Windows (the build is verified on macOS; a WPF window is not)
- [ ] PR `feature/wpf-record` -> `dev`; release 4.1.0

## Done

- [x] Analysis and probes (brief, "Evidence"); feature folder (2026-11-05)
- [x] Fixture: a WPF library (pass-one window with code-behind, pass-two page rooted in its own type, a `Resource` file) referencing a library; `net8.0-windows` and `net472`
- [x] Markup run in `Import.record`: references resolved in a run of their own, the referenced outputs (`OutOf`) placed in a per-run `OutDir`, the design-time build with the compiler on and `ProduceReferenceAssembly=false`; the up-front refusal removed
- [x] A reference to a project an earlier import recorded (its lock exists) is allowed: WPF projects are recorded in a second `Import.record`
- [x] Missing referenced output: the import fails naming the file and the project
- [x] `Page`/`ApplicationDefinition`/`Resource` into `Evaluation.Imports` (UseWPF projects only); build and `check` say "run the import again"
- [x] `Compilation.Resources`: `.resx` outputs only (the `g.resources` is an `EmbeddedResource` with an `OutputResource` too)
- [x] `restoreProps`: `xake-record/<key>/` appended once; a `_wpftmp` project reads its NuGet props from `BaseIntermediateOutputPath` (`net472`: MSB3644 otherwise)
- [x] `net472` markup on macOS: works (the test covers it)
- [x] Tests: record + build from the lock, stale after a XAML edit, `Page`/`UseWPF` alone unchanged; full suite green
- [x] Docs: release notes 4.1.0, `lock.md`, `workflows.md` ("WPF projects")
- [x] A consumer solution (two brands, two WPF projects of 93) recorded, built and packed on macOS from a local 4.1.0 package; a second import changes no file
