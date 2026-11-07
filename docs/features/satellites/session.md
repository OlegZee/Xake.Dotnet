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

1. The lock's per-culture section (`Lock.fs`) with a write/read round trip, then the import,
   then the satellite rule.
