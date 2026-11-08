# Tracker: satellites

One line per item. Status as of 2026-11-07: started.

## Open

- [ ] PR #15 `feature/satellites` -> `dev` (release 4.3.0): CI green (4/4); merge

- [ ] Push the redesign (local commits) to PR #15 when the owner says so
- [ ] Consumer check of `4.3.0-sat.3` (the redesigned API: `Locks.satellite` with `SatelliteInputs`, `satellite { }`)

## Later

- [ ] Run folders: a referenced lock's satellites by name; `deps.json` `"resources"` lines (stand-ins in the app pass)

## Done

- [x] Redesign implemented (decisions 9-24): core `Satellite` and `satellite { }` in Xake.Dotnet, `.restext`, `Locks.satellite` with `SatelliteInputs`, lock = cultures only, removals, docs, locks; byte-identical on SDK 8 and 10, both layers equal (2026-11-08)
- [x] Probe G (Windows 11, SDK 8.0.301/9.0.304): satellite attributes equal macOS's except FileVersion for hand-written attributes without one; empty-string attributes left out; macOS-built (SDK 10) satellites load on .NET Framework 4.8; another token falls back silently (`probe.md`, G, 2026-11-08)
- [x] Critic's review of the redesign; accepted as decisions 16-24 (brief, 2026-11-08)
- [x] Consumer check of `4.3.0-sat.2` (macOS, SDK 8.0.427, two brands): locks unchanged against sat.1; the orphan warning fires on exactly the one known project; `satelliteWith` over the whole translation table (19 folders, files via `getFiles` in the rule): 86 satellites per brand, version, culture and public key token of the main assembly, every resource named `*.<culture>.resources`, as many resources as translation files; the `.resources` streams byte-identical to the consumer's own per-culture project (whose net8 satellite even lacks `AssemblyCulture`); `ResourceManager` finds the strings (net8.0, netstandard2.0); full build, check, pack green (2026-11-08)
- [x] `Locks.satelliteFrom` over resource sets (`resourceset`, `getFiles`: an added translation rebuilds); the example rule on a typed target (2026-11-08)
- [x] `Locks.satelliteWith` (translations outside the project; decision 8): recipe derived at build time, merge with a recorded culture; the import warns about a culture resource without a neutral one; the pure part moved to `Satellite.fs`; tests on SDK 8 and 10 (byte-identical, net472 and net8.0, signed, version with a suffix; `ResourceManager`; merge and duplicate) (2026-11-08)
- [x] Consumer check on `4.3.0-sat.1` (macOS, SDK 8.0.427, two brands, ~90 projects each): re-import changed only the locks of the localized projects (`Satellites` added), the rest byte-identical; full build, check, pack with obfuscation green; satellites `cmp`-identical to `dotnet build` (net472, netstandard2.0; ja, zh-CN), the main assembly's version and public key token, `ResourceManager` finds the strings (2026-11-08)
- [x] `Pack.fromNuspec` (a wildcard per culture folder) and the SBOM take the satellites (test); `build.fsc.fsx build check` green (2026-11-08)
- [x] Build: `Locks.satellite` (on request) and `Locks.satellitesOf`; the SDK's recipe read at import (`Satellites.References`, `/pathmap`, `/features`); byte-identical to `dotnet build` with generated and hand-written attributes; docs (lock.md, pack.md, README, release notes) (2026-11-08)
- [x] Import: `Recorded.Satellites` from the culture resources, the main compile's switches and its generated assembly info; F# reported and left out; invariant globalization fails; fixture test against `dotnet build` (2026-11-07)
- [x] Lock: `Recorded.Satellites` (`Options`, `Attributes`, `Cultures`), written only when present, round trip and `mapText` tested; `lock.md` (2026-11-07)
- [x] Probe: signing (key file, public, delay): the satellite takes the main compile's key switches, tokens equal, composed signed satellite byte-identical (`probe.md`, E, 2026-11-07)
- [x] Decide: a separate satellite rule, requested explicitly; own satellites only, referenced ones later; composed path keeps its refusal; release 4.3.0, #12 untouched (brief, 2026-11-07)
- [x] Decide: compose the satellite `Csc` by the SDK recipe; attributes from `Generated`, else the built main assembly (brief, decision 4, 2026-11-07)
- [x] Probe: the satellite under the record (SDK 8.0.427, 10.0.403): not capturable, composed one byte-identical (`probe.md`, 2026-11-07)
- [x] Branch `feature/satellites` from `dev` (`51baf23`); feature folder (2026-11-07)
