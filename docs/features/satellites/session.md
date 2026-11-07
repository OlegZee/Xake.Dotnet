# Session: satellites

Branch `feature/satellites` from `dev` at `51baf23` (after 4.2.0). Goal and design: `brief.md`;
items: `tracker.md`.

## State at 2026-11-07

- Feature folder created; nothing implemented. The brief collects the earlier analysis
  (`docs/features/hermetic-windows/analysis/lock-content.md` §3) and the owner's round-4
  answers; S71 is reopened by starting this branch.

- A peer probe found that the satellite's attributes are read from the built main assembly
  (brief, "Risks"); confirmed in the SDK 8.0.427 and 10.0.403 targets. The probe below must
  check what the design-time record yields for the satellite.

- Probe done (`probe.md`): the satellite's `Csc` cannot be captured by the design-time record
  (`GetAssemblyAttributes` needs the main dll; the satellite `Csc` ignores `SkipCompilerExecution`).
  Composed from the main `Generated` assembly info it is byte-identical; hand-written attributes
  come from the built dll's file version info. The probe's scripts are not kept in the repository.

## Next step

- The owner chose to compose the satellite's `Csc` call (brief, decision 4).

- The owner: a separate satellite rule the script requests explicitly; own satellites only
  (referenced ones in run folders later); the composed path keeps its refusal; release 4.3.0,
  #12 untouched. No open decisions left.

- Signing probed (`probe.md`, E): the satellite's key switches are the main compile's; tokens
  equal in all three modes; a composed signed satellite is byte-identical.

- Lock format done: `Lock.Satellites` in `Recorded` (`src/hermetic/Lock.fs`), tests in
  `RecordTests.fs`; hermetic suite green (336).

- Import done (`src/hermetic/Import.fs`: `satelliteAttributesOf`, `satelliteOptions`,
  `cultureSuspects`, in `entryOf`; three properties added to the dump). Tests:
  `Satellite composition`, `Import record satellites` (the attributes equal `dotnet build`'s
  satellite source, the commit a token). Hermetic suite green (340).

- Build done (`Locks.satellite`, `Locks.satellitesOf`, `satelliteSource`,
  `satelliteAttributesFromAssembly`). SDK 10 differs from 8: the satellite takes only
  `mscorlib`/`netstandard`/`System.Runtime` and gets `PathMap`/`Features`; the import reads
  the recipe off the recording SDK's targets (`Import.satelliteRecipeOf`, `Satellites.References`).
  Test `a satellite built from the lock is msbuild's, byte for byte` (generated and hand-written
  attributes). Suites green: hermetic 343, dotnet 151.

1. `Pack` and SBOM with satellites (a test), then the PR to `dev` (4.3.0). A consumer check
   (re-import and build of a localized project) before the release.
