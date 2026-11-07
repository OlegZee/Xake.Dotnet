# Tracker: satellites

One line per item. Status as of 2026-11-07: started.

## Open

- [ ] Probe: Windows `FileVersionInfo` of hand-written attributes
- [ ] PR `feature/satellites` -> `dev` (release 4.3.0), CI green
- [ ] Consumer check on a preview package (`4.3.0-sat.1`): a localized project re-imported, satellites built, translations found
- [ ] Windows: hand-written attributes (`FileVersionInfo`) not checked

## Later

- [ ] Run folders: a referenced lock's satellites by name; `deps.json` `"resources"` lines (stand-ins in the app pass)

## Done

- [x] `Pack.fromNuspec` (a wildcard per culture folder) and the SBOM take the satellites (test); `build.fsc.fsx build check` green (2026-11-08)
- [x] Build: `Locks.satellite` (on request) and `Locks.satellitesOf`; the SDK's recipe read at import (`Satellites.References`, `/pathmap`, `/features`); byte-identical to `dotnet build` with generated and hand-written attributes; docs (lock.md, pack.md, README, release notes) (2026-11-08)
- [x] Import: `Recorded.Satellites` from the culture resources, the main compile's switches and its generated assembly info; F# reported and left out; invariant globalization fails; fixture test against `dotnet build` (2026-11-07)
- [x] Lock: `Recorded.Satellites` (`Options`, `Attributes`, `Cultures`), written only when present, round trip and `mapText` tested; `lock.md` (2026-11-07)
- [x] Probe: signing (key file, public, delay): the satellite takes the main compile's key switches, tokens equal, composed signed satellite byte-identical (`probe.md`, E, 2026-11-07)
- [x] Decide: a separate satellite rule, requested explicitly; own satellites only, referenced ones later; composed path keeps its refusal; release 4.3.0, #12 untouched (brief, 2026-11-07)
- [x] Decide: compose the satellite `Csc` by the SDK recipe; attributes from `Generated`, else the built main assembly (brief, decision 4, 2026-11-07)
- [x] Probe: the satellite under the record (SDK 8.0.427, 10.0.403): not capturable, composed one byte-identical (`probe.md`, 2026-11-07)
- [x] Branch `feature/satellites` from `dev` (`51baf23`); feature folder (2026-11-07)
