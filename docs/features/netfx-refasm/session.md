# Session: netfx-refasm

Branch `feature/netfx-refasm` from `dev` at `cc6e3de`. Task B-5: the import takes .NET Framework
reference assemblies from `Microsoft.NETFramework.ReferenceAssemblies` on every OS. Goal and the
owner's decisions: `brief.md` (its "Decisions" section: the root hidden by an ordinary property,
D1 (a)). PR #12 (`feature/import-file-casing`) also touches `Import.fs`: not touched here.

## State at 2026-11-19

Implemented, committed locally, not pushed.

- `Import.fs` +43/-2 (budget 25; about half is doc comments): `NetFxReferenceAssemblies`
  (`RequireQualifiedAccess`: `Installed` is a `PacksFrom` case too), `RecordOptions`
  `NetFxReferenceAssemblies` (default `Package`) and `NetFxReferenceAssembliesVersion` (`1.0.3`),
  three globals in `recordGlobals` (script's own value wins), one `PropertyGroup` in `restoreProps`.
- The probe's first condition (`TargetFramework` is `.NETFramework`) missed a single-framework
  project: its restore evaluates without `TargetFramework`, so the restore did not add the
  package and the references came from `Program Files` (seen on the VM). The root is now also
  hidden where `TargetFramework` is empty; `net8.0`/`netstandard2.0` references are the same with
  it (VM) and their locks byte-identical (test, multi- and single-framework).
- Tests +90 (`NetFxReferenceTests.fs` 67: package + CPM + GAC-only reference + build, configured
  version, other frameworks byte-identical; `RecordTests.fs` +22: `recordGlobals`).
- Docs +45 (`lock.md`, `hermetic-mode.md`, release notes "Unreleased").
- Windows acceptance (`windows/`, ps1 24 + fsx 32 + README 33 + fixture ~30): on the VM with
  packs v4.0 to v4.8.1, SDK 10.0.404 and 8.0.428: `Package` records `pkg:` references and builds,
  `Installed` fails naming `Program Files`; the locks are byte-identical to the macOS ones (SDK
  10.0.404, outside a git checkout).
- `dotnet fsi build.fsx -- -- build test pack`: all green (532 tests in 8 slices).
- fsc self-build: no update needed; `build.fsc.fsx` makes its locks from descriptions
  (`Locks.generate`), not `Import.record`, so these globals never reach it.
- Seen on the way: an import under a path with `--` fails (`--` rewritten to `__`), as in lock-inputs.

## Next step

The owner's review. On their word: push `feature/netfx-refasm`, PR to `dev` (squash).
