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

## State at 2026-11-08 (night): the redesign implemented, local only

- Commits on `feature/satellites`, **not pushed** (owner: push only when told): docs from the
  brief (`lock.md`, `tasks.md` "satellite", release notes); the core `Satellite` in `src/dotnet`
  (template read with `System.Reflection.Metadata` 8.0.0, a new package dependency: version,
  attributes, public key, framework, `/optimize` from `DebuggableAttribute`, resource names;
  checks of culture, key, token, neutral names); `satellite { }` (`Dotnet.satellite.fs`);
  `.restext` compiled (`ResxFormat.readText`, also for `csc { }`); hermetic: `Lock.Satellites`
  = cultures only (preview fields read past), the import without SDK targets,
  `Locks.satellite settings lockPath fwk SatelliteInputs`; `satelliteWith`/`satelliteFrom` and
  `src/hermetic/Satellite.fs` removed; descriptions and locks updated (`import update-locks`,
  SDK 10.0.403), `build.fsc.fsx build check` green.
- Verified: byte-identical to `dotnet build` on SDK 8 **and** 10 (net472, netstandard2.0, net8.0,
  signed; generated and hand-written attributes; Debug template); `satellite { }` and
  `Locks.satellite` give the same bytes; probe G (Windows, `probe.md`).
- Found while implementing: ICU lists `zh-Hans-CN`, not `zh-CN` (`Satellite.isCulture` also
  takes a name with a known LCID); a netstandard2.0 `mscorlib` is a facade (`satellite { }`
  takes those of the three references the framework has).
- Not tested: `publicsign`/`delaysign` of `satellite { }`; an F# entry's `Locks.satellite` refusal.

## Next step

1. When the owner says so: push to PR #15, CI; preview `4.3.0-sat.3` to the consumer's session
   (re-import changes the locks of localized projects once more; `satelliteWith`/`satelliteFrom`
   callers move to `Locks.satellite ... { SatelliteInputs.Default with Resources = ... }`).
2. Windows: run the satellite tests on the VM (`ssh win11`, SDK 8.0.301/9.0.304) if wanted.
3. SDK-independent locks (the `Sdk`, pack versions) are a separate brief.
