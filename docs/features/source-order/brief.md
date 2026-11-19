# Brief: source order independent of the importing OS (release 4.4.0)

Status 2026-11-19: approved and implemented.

## Finding

A consumer re-imported 46 projects (x2 configurations) with 4.4.0-preview.1 on macOS (SDK 8.0.427)
and on Windows ARM64 (SDK 8.0.428): 92 locks differ in `Compilation/Sources` and
`Recorded/Placed` (same files, other order; one project 29 `Sources` entries on Windows, 3 on
macOS), 4 test-project locks in the order of `Compilation/Options` values.

## Cause (probed: macOS SDK 8.0.427 and 11.0.100, Windows VM SDK 8.0.428)

- msbuild sorts each glob's files itself: `EngineFileUtilities.GetFileList` ends with
  `Array.Sort(fileList, StringComparer.OrdinalIgnoreCase)` -- on the path as the OS spells it.
  The file system's enumeration order plays no part (APFS `readdir` is unsorted; the result is sorted).
- The separator decides: `/` (0x2F) sorts before digits and letters, `\` (0x5C) after every
  letter (OrdinalIgnoreCase compares upper case: `A`-`Z` are 0x41-0x5A) and before `_`. Probe
  `ord1`, 16 files: macOS `Abc.cs, Abc/x.cs, ..., lower/m.cs, Lower2.cs`; Windows
  `..., Lower2.cs, lower\m.cs`. Any `Folder/X.cs` beside a `FolderSomething.cs` swaps
  (`Models/A.cs` vs `ModelsExtensions.cs`) -- common in a real project.
- `Recorded/Placed` is the list of sources msbuild compiles out of the masks' order, each after
  its predecessor (`Lock.placedSources`). The masks' order (`Lock.canonicalSources`) is msbuild's
  sort with `/`, so on macOS/Linux only the generated files are placed (the "3"); on Windows every
  separator-sensitive pair breaks the run and is placed too (the "29"). Not files copied beside
  the lock -- that is `binaryDir`.
- The same sort is behind the `Options` difference: item switches from globs. Probe `t2` (an xunit
  project with `EmbeddedResource`/`AdditionalFiles` globs): `/resource:` and `/additionalfile:`
  values are in another order on Windows; every other switch is in the same place (the `Csc`
  task writes its switches in a fixed order). (To confirm against one of the consumer's 4 locks.)

## The reference build conflicts

`dotnet build` on Windows hands csc the `\`-sorted order, on macOS the `/`-sorted one: the plain
SDK build itself gives other dll bytes per OS for such a project (type and resource order in the
metadata). "Byte-for-byte equal to `dotnet build`" and "one lock on every OS" cannot both hold
for these projects; the lock has to pick one order.

## Proposed rule

- The lock records glob-expanded items in the `/` order (OrdinalIgnoreCase on the `/` spelling),
  which is what `Lock.canonicalSources` already calls the masks' order: macOS/Linux imports do
  not change at all (the transform is the identity where the separator is `/`); a Windows import
  is rewritten into that order. Explicit items keep their place.
- Equality with `dotnet build` then holds where msbuild's separator is `/` (every existing
  byte-equality test: the hermetic tests run on Linux CI and macOS only); on Windows a build from
  the lock differs from `dotnet build` exactly for separator-sensitive projects. Documented in `lock.md`.
- Alternative (owner's call): the `\` order everywhere, to match Visual Studio/Windows builds.
  It changes every existing lock and breaks the Linux/macOS byte-equality tests; not proposed.

## Design (code ~45 lines, tests ~70, docs ~15)

1. `Lock.fs` (~20): `canonicalSources` takes the comparison key (`canonicalSourcesBy key`;
   `canonicalSources = canonicalSourcesBy id`); the longest-run helper is lifted out of
   `placedSources`; new `Lock.portableSources (masks) (separator) (isGenerated) (sources)`:
   identity for `/`; else the run of user sources in msbuild's native order (ranked by
   `canonicalSourcesBy (replace '/' with separator)`) keeps its positions in the list and is
   refilled in the `/` order. Generated files and exceptions keep their positions (exact: same
   positions as on macOS, since the set and the glob layout are the same).
2. `Lock.fs` (~12): `portableItemSwitches separator switches options`: within each block of
   consecutive values of one item switch (`resource`, `linkresource`, `additionalfile`), every
   maximal run ascending in the native order is sorted in the `/` order. No masks exist for these
   items, so a hand-listed block that happens to be `\`-ascending is sorted too (rare; documented).
   Not a sort of all `Options`: their order is the task's fixed order and the same on both OSes,
   and sorting would change every lock.
3. `Import.fs` `entryOf` (~10): applied to C# sources (masks: rule 1; C# without matching masks:
   rule 2's run sort) and to the `Options`; F# sources untouched (their order is explicit and
   meaningful). The separator is `Path.DirectorySeparatorChar`, a parameter of an internal
   `entryOfWith` for the tests.
4. No lock format change. Locks recorded on macOS/Linux (this repository's included) do not
   change; Windows-recorded locks change once on re-import (fine before 4.4.0).

## Tests

- Pure: the 16-file probe set fed in the Windows order with `\` and in the macOS order with `/`
  gives the same `Sources` and the same `Placed`; with generated files and an explicit
  out-of-order item; identity for `/`.
- `entryOfWith` over a dump with items in the two orders: equal entries and equal lock text.
- Options: `/resource:` and `/additionalfile:` blocks of the probe `t2` in both orders.
- Windows VM: a glob-heavy project imported on the VM and on macOS gives equal locks.

## Out of scope (not investigated further)

- Test projects on Windows: testhost x64 files in `App/Content`, `win-x64`/`win-x86` targets in
  `deps.json`; PresentationBuildTasks version (an SDK patch difference).
