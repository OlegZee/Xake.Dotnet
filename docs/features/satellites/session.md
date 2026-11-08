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

## State at 2026-11-08 (evening)

- On the branch (PR #15, not merged): `Recorded.Satellites` with Options/References/Attributes/
  Cultures, `Locks.satellite`, `Locks.satelliteWith`, `Locks.satelliteFrom`, `src/hermetic/Satellite.fs`;
  previews `4.3.0-sat.1` and `sat.2` passed the consumer's checks.
- The owner agreed a redesign (brief, "Redesign", decisions 9-15): two layers (`satellite { }` in
  Xake.Dotnet, `Locks.satellite` with `SatelliteInputs`), SDK 10's recipe for every SDK,
  attributes from the main assembly's metadata, the lock keeping cultures only, a token check.
  Not implemented. The session that agreed it grew too large; continue in a fresh one.

- A critic (fresh eyes) reviewed the redesign; the owner accepted all of it: brief, decisions
  16-24 (per-OS parity, the neutral-name check with a warn/fail setting, the token compared on
  the output, the culture checked, no reference fallback, culture suffix stripped for every
  extension, `satellite { }` from its target and template, F# later) and the extended test list.

## Next step

1. Rewrite `docs/hermetic/lock.md` "Satellite assemblies" (and `docs/tasks.md` for `satellite { }`,
   release notes) from the brief's API examples and name table.
2. Implement: the core and `satellite { }` in `src/dotnet` (move `Satellite.fs`), `Locks.satellite`
   with `SatelliteInputs`, the import writing cultures only, the removals; adapt the tests (brief,
   "Tests to keep or adapt"); `build.fsc.fsx import update-locks` when source files move.
3. Suites, fsc `build check`, push to PR #15; preview `4.3.0-sat.3` to the consumer's session for
   the same check as sat.2.
4. Windows unchecked; SDK-independent locks (the `Sdk`, pack versions) are a separate brief.
