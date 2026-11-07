# Tracker: satellites

One line per item. Status as of 2026-11-07: started.

## Open

- [ ] Probe: Windows `FileVersionInfo` of hand-written attributes
- [ ] Build: a separate satellite rule, requested explicitly, writing `<out>/<culture>/<asm>.resources.dll` (culture, version, public key token of the main assembly)
- [ ] Signing, `Pack`, SBOM take the satellites
- [ ] Docs: README (S71), `lock.md`, `pack.md`, `description.md`, release notes
- [ ] PR `feature/satellites` -> `dev` (release 4.3.0)

## Later

- [ ] Run folders: a referenced lock's satellites by name; `deps.json` `"resources"` lines (stand-ins in the app pass)

## Done

- [x] Import: `Recorded.Satellites` from the culture resources, the main compile's switches and its generated assembly info; F# reported and left out; invariant globalization fails; fixture test against `dotnet build` (2026-11-07)
- [x] Lock: `Recorded.Satellites` (`Options`, `Attributes`, `Cultures`), written only when present, round trip and `mapText` tested; `lock.md` (2026-11-07)
- [x] Probe: signing (key file, public, delay): the satellite takes the main compile's key switches, tokens equal, composed signed satellite byte-identical (`probe.md`, E, 2026-11-07)
- [x] Decide: a separate satellite rule, requested explicitly; own satellites only, referenced ones later; composed path keeps its refusal; release 4.3.0, #12 untouched (brief, 2026-11-07)
- [x] Decide: compose the satellite `Csc` by the SDK recipe; attributes from `Generated`, else the built main assembly (brief, decision 4, 2026-11-07)
- [x] Probe: the satellite under the record (SDK 8.0.427, 10.0.403): not capturable, composed one byte-identical (`probe.md`, 2026-11-07)
- [x] Branch `feature/satellites` from `dev` (`51baf23`); feature folder (2026-11-07)
