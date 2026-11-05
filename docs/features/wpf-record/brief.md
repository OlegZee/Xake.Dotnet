# Brief: wpf-record (S69)

Record a WPF project (`UseWPF` with XAML `Page`/`ApplicationDefinition` items) into a lock, so
that it builds like every other entry: csc from the lock, no msbuild at build time.

## Today

`Import.record` refuses such a project up front (`src/hermetic/Import.fs`, "WPF is not supported
by the import yet"; test `RecordTests.fs`). The cause, as the comment says: the record runs
msbuild's design-time build (`SkipCompilerExecution=true`, `BuildProjectReferences=false`), and the
markup compiler cannot run there:

1. **Pass 2 needs a compiled assembly.** XAML whose root or members are the project's own types
   is compiled after a temporary assembly (`GenerateTemporaryTargetAssembly` builds
   `<project>_<random>_wpftmp.<ext>` through the build engine). That project inherits the global
   properties, so `SkipCompilerExecution=true` reaches it too: no assembly, `MC3050`.
2. **The markup compiler loads the whole reference closure.** With `BuildProjectReferences=false`
   the referenced projects' outputs do not exist (`MC3050`/`MC1000` for a missing assembly).

## What is already in place

Everything after the markup compiler (`entryOf`):

- `GeneratedInternalTypeHelper.g.cs` (and any `*.g.cs`) under the intermediate folder is a text
  input: `Compilation.Generated`;
- `<AssemblyName>.g.resources` (the BAML of every page, the `Resource` items) is "any other
  generated file": copied byte for byte to `<lock>.files/<tfm>/`, the `/resource:` switch names the
  copy (`docs/hermetic/lock.md`);
- the `.resources` of the `.resx` files: `Compilation.Resources`;
- a precedent for a second msbuild run over a subset of projects: the application run (runtime
  config, deps.json, binding redirects) with its own `OutDir` and stand-ins of the referenced
  outputs (`stubAssembly`);
- a precedent for inputs that are not msbuild files: `appSources` (`App.config`,
  `runtimeconfig.template.json`) go into `Evaluation.Imports`, and an edit fails the build with
  "run the import again" (`Locks.staleImports`) and `check`.

No lock format change is needed.

## Proposal

1. **A markup run** for the projects that compile markup, after the design-time run's
   configuration restore, modelled on the application run:
   - a global `OutDir` per run, where every referenced project's **real** output is placed
     (hard link or copy from `RecordOptions.OutOf`), instead of a stand-in; a missing output fails
     the import naming it ("build the references first");
   - `SkipCompilerExecution` not set for this run, so the temporary assembly compiles;
     `ProvideCommandLineArgs=true` still reports the main compilation's command line;
   - the entry is then made by `entryOf` as for any project.
2. **Staleness**: the `Page`, `ApplicationDefinition` and `Resource` items go into
   `Evaluation.Imports` (text-hashed like `appSources`): an edited `.xaml` or image fails the build
   with "run the import again".
3. **Tests**: replace the refusal test with a fixture whose XAML root is the project's own type
   (pass 2), recorded and built from the lock; the recorded `.g.resources` is byte-equal to
   msbuild's.
4. **Docs**: README "a WPF project is refused up front"; `lock.md` already describes the copy.

## Evidence (probes on macOS, SDK 8.0.427, outside this repository)

- The markup compiler for `net8.0-windows` runs on macOS (`EnableWindowsTargeting`): a synthetic
  project referencing the built outputs by path produced `<Asm>.g.resources` and
  `GeneratedInternalTypeHelper.g.cs` offline (no private feed), pass 1 and pass 2.
- csc from the lock's kind of inputs (sources, `GeneratedInternalTypeHelper.g.cs`,
  `/resource:<Asm>.g.resources`) built a valid assembly with the BAML embedded, 0 errors.
- The BAML names the referenced assemblies with their version (`<Name>, Version=...`).
- Not probed: `net472` markup on macOS; the record itself.

## Open decisions

- **Identities of the referenced assemblies.** The BAML pins `Version=` of every referenced
  assembly. A build whose referenced project changed version without a new import compiles, but
  XAML may fail to load at run time (strictly on .NET Framework with strong names). Options: record
  the referenced identities in the entry and verify them at build time (a new lock field); or
  document it and rely on the version source being an import input.
- **Ordering in a consumer script.** The markup run needs the referenced outputs, so a script
  imports the other projects, builds them, then imports the WPF ones (two `Import.record` calls),
  or `record` itself `need`s the `OutOf` outputs (the rules that build them read locks the same
  call has to write first). The two-call form is the first step.
