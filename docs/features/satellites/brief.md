# Brief: satellites (S71)

Build satellite assemblies (`<out>/<culture>/<AssemblyName>.resources.dll`) for the
culture-specific resources of a project (`Strings.de.resx`, `Logo.fr-FR.png`), and place a
referenced project's satellites in an application's run folder, so that a localized
application builds from locks like any other.

Reopens S71 ("Satellite assemblies: deferred by decision", `docs/hermetic/README.md`). The
analysis is in `docs/features/hermetic-windows/analysis/lock-content.md` §3; the owner's
answers in `docs/features/hermetic-windows/interview.md`, round 4.

## Today

- **Imported locks** (`src/hermetic/Import.fs`): an `EmbeddedResource` with `WithCulture` (or a
  culture suffix msbuild would see) is dropped with a report ("a satellite assembly, not
  built"); the resx pairs of the main assembly skip culture items.
- **Composed path** (`src/dotnet/ProjectPlan.fs`, `cultureOf`): a culture-specific resource in a
  description fails the build ("satellite assemblies are not built by the composed path").
- **Run folders**: only the `Related` files beside a referenced assembly are copied, not its
  culture subfolders; `deps.json` has no `"resources"` lines for them.
- **Package satellites already work**: copy-local items with a `DestinationSubDirectory`
  (`App.Copy`), listed by SBOM tier 1, taken by `Pack` with a wildcard per folder.
- Docs listing satellites as not covered: `docs/hermetic/README.md`, `lock.md`, `pack.md`,
  `description.md`, `docs/description.md`.

## Decided (interview round 4, 2026-10-26)

1. ~~Capture msbuild's satellite `Csc` call, do not compose it~~: superseded by 4 below (the
   probe showed the call cannot be captured).
2. A culture-suffixed resx under invariant globalization fails the import (the same project
   would otherwise give a different lock).
3. C# first; F# when a consumer needs it.
4. **Compose the satellite's `Csc` call by the SDK target's recipe** (`CoreGenerateSatelliteAssemblies`),
   from inputs and settings the record reads (owner, 2026-11-07, after `probe.md`). The
   attributes come from the main entry's `Generated` assembly info (without
   `AssemblyConfiguration`, plus `AssemblyCulture`) when the project generates assembly info,
   else from the built main assembly at build time, as msbuild's `GetAssemblyAttributes` does.
5. The composed path (`xake.project.json`, `ProjectPlan`) keeps its refusal of culture-specific
   resources (owner, 2026-11-07).
6. Scope (owner, 2026-11-07): a project's own satellites, built **by a separate rule the script
   requests explicitly**; building the main assembly does not build them. The import records
   their inputs always (a lock without culture resources is unchanged). A referenced project's
   satellites in a run folder and the `deps.json` `"resources"` lines: **not now**.
7. Release: 4.3.0.
8. **Translations kept outside the project** (owner, 2026-11-08; the consumer's brief): in 4.3.0
   too. `Locks.satelliteWith settings lockPath fwk resources` with (file, manifest name) pairs.
   The recipe of an entry without `Recorded.Satellites` is **derived at build time** (the lock
   does not change; recording it for every C# entry would change every lock). A culture the lock
   records **merges** with the script's resources; a duplicate manifest name fails. `feature/import-file-casing` (#12) is not touched by this feature (owner,
   2026-11-07).

## Redesign (owner, 2026-11-08) -- supersedes the shape below and decisions 4 (attributes) and 8 (API)

Breaking changes are fine: nothing of this is released. Principle (also for later work): a recipe
is a pure function with one task over Xake's primitives (`resourceset`, filesets); layout
conventions and discovery (which cultures, which folders) are the script's, shown in the docs.

9. **Two layers.**
   - **Xake.Dotnet (`src/dotnet`)**: a `satellite { }` task beside `csc { }`, no lock. The pure
     core lives here too (`Satellite` module, moved from `src/hermetic/Satellite.fs`): compiler +
     references + switches + attributes + culture + resources -> a `Csc` compilation.
   - **Hermetic**: `Locks.satellite settings lockPath fwk (inputs: SatelliteInputs)` over the same
     core; the compiler, the references, the key and other switches, the restore, the
     prerequisites and the `HERMETIC=on` gate come from the lock entry.
   - `Locks.satelliteWith` and `Locks.satelliteFrom` are removed.
10. **One recipe for every SDK: SDK 10's.** References `mscorlib`, `netstandard`,
    `System.Runtime` (filtered from the entry's or the toolchain's), `/pathmap` and `/features`
    passed on. No SDK targets are read, at import or at build time; no installed SDK is needed for
    a satellite. Consequence: on SDK 8 a satellite is not byte-identical to `dotnet build`'s (SDK 8
    hands it every reference); version, culture, token and resources are the same. To be put to the
    critic when the finished solution is reviewed.
11. **Attributes are read from the built main assembly's metadata** (`System.Reflection.Metadata`,
    the same code on every OS), never from Win32 resources and not from the lock's generated
    assembly info. The rules of the .NET (Linux/macOS) reader `GetAssemblyAttributes` uses, as
    probed: Company, Description, FileVersion, Product from their attribute when present;
    Copyright and Title from theirs, else `" "`; InformationalVersion from its attribute, else the
    assembly version; Version = the assembly version. Linux containers are the main scenario. Any
    attribute can be given explicitly and then replaces (or adds to) the read one. (csc writes the
    satellite's own Win32 version resource from its attributes on every OS: Explorer shows it.)
12. **The lock records only what msbuild knew**: `Recorded.Satellites` = the resources per culture
    (`Cultures`). `Options`, `References`, `Attributes` go; the switches come from the main compile
    at build time (`DisabledWarnings` is lost: rare, no effect on the bytes). The import stops
    reading SDK targets. Locks of localized projects change once more (re-import).
13. **Load guarantee**: the satellite's public key token must equal the template's (the main
    assembly's); a mismatch fails the build. The token comes from the key switches, not from the
    template, so without a lock the key is given explicitly.
14. **`resources`** are `ResourceFileset list` (`resourceset { prefix; basedir; includes; dynamic }`),
    read with `getFiles` (an added translation rebuilds); a file is named as `resourceset` names it,
    the target's culture put in. The lock's resources of the culture always go in too; a manifest
    name twice fails. No file and no recorded resource fails.
15. **`Locks.satellitesOf settings lockPath fwk dir`** only reads the lock: the cultures it
    records. The script lists external cultures itself.

### API (agreed examples; the docs to be rewritten from these)

```fsharp
open Xake.Typed
type SatelliteG = { cfg: string; fwk: string; name: string; culture: string; asm: string }
let Satellite = Target<SatelliteG> "out/(cfg:*)/(fwk:*)/(name:*)/(culture:*)/(asm:*).resources.dll"

// 1. no lock
Satellite.Rule (fun m ->
    satellite {
        template $"out/{m.cfg}/{m.fwk}/{m.name}/{m.name}.dll"   // version and attributes
        targetfwk m.fwk                                         // references as csc {} finds them
        keyfile "key.snk"                                       // or publicsign / delaysign
        resources (resourceset { prefix "Lib"; basedir $"translations/{m.culture}"; includes "**/*.resx" })
        attribute "AssemblyCopyright" "(c) 2026 Example"        // optional: replaces the read one
    })

// 2. a lock, the project's own culture resources
Satellite.Rule (fun m -> Locks.satellite settings (lockOf m.name m.cfg) m.fwk SatelliteInputs.Default)

// 3. a lock plus translations kept outside the project, an attribute given
Satellite.Rule (fun m ->
    let translations =
        match rootNamespaces.TryFind m.name with
        | Some ns -> [ resourceset { prefix ns; basedir $"translations/{m.name}/{m.culture}"; includes "**/*.resx" } ]
        | None -> []
    Locks.satellite settings (lockOf m.name m.cfg) m.fwk
        { SatelliteInputs.Default with Resources = translations; Attributes = [ "AssemblyFileVersion", "4.3.0.512" ] })

// 4. which satellites: the script's decision
command "satellites" {
    let! own = Locks.satellitesOf settings (lockOf "Lib" "Release") "net8.0" (outOf "Release" "net8.0" "Lib")
    let external = [ for c in [ "ja"; "zh-CN" ] -> Satellite.Format { cfg = "Release"; fwk = "net8.0"; name = "Lib"; culture = c; asm = "Lib" } ]
    do! need (own @ external |> List.distinct) }
```

Names (`prefix "Lib"`, `basedir "translations/ja"`): `Strings.resx` -> `Lib.Strings.ja.resources`;
`Forms/Main.resx` -> `Lib.Forms.Main.ja.resources` (`dynamic true`, the default) or
`Lib.Main.ja.resources` (`dynamic false`); `Strings.ja.resx` -> `Lib.Strings.ja.resources`;
`Logo.png` -> `Lib.Logo.png`.

Open in the implementation: whether `satellite { }` can take the culture and output from the
rule's target (as `csc { }` takes `out`), and how `targetfwk` finds the three references (the
toolchain discovery `csc { }` uses).

### Tests to keep or adapt

- Byte-identical to `dotnet build` on SDK 10 (own resources; external translation; net472 and
  net8.0; signed; generated and hand-written attributes); SDK 8: version, culture, token,
  resources, `ResourceManager`.
- Merge with a recorded culture and a duplicate name; an added file rebuilds; an empty set fails;
  a token mismatch fails; an explicit attribute replaces the read one.
- `satellite { }` without a lock: the same satellite as `Locks.satellite` for the same inputs.
- A lock without culture resources unchanged; `Recorded.Satellites` round trip (cultures only).

## Shape before the redesign (history)


- Per culture in the entry: the culture resx -> `.resources` pairs (compiled by the existing
  `Resx`) with their manifest names, and the settings the target reads (`Optimize`,
  `WarningLevel`, `Deterministic`, warnings as errors, disabled warnings; the key switches are the
  main entry's). Compiler and references are the main entry's; the switches are the target's
  fixed set (`probe.md`, C).
- The satellite rule (decision 6) writes `<out>/<culture>/<asm>.resources.dll`;
  `AssemblyVersion` and the public key token equal the main assembly's.
- Later (not now, decision 6): run folders take each referenced lock's satellites by name from
  the lock; `deps.json` `"resources"` lines from stand-in satellites in the app pass.
- Signing takes satellites like any other built file; `Pack` and the SBOM pick them up.
- Lock format: a new optional field, no bump; a lock without culture resources is
  byte-identical to today's.

## Open decisions

(none)

## Risks

- The `Csc` parameters of `CoreGenerateSatelliteAssemblies` differ between SDK 8 and 9/10:
  capture, never guess.
- **The satellite's attributes come from the built main assembly.** `CoreGenerateSatelliteAssemblies`
  depends on `_GetAssemblyInfoFromTemplateFile`: `GetAssemblyAttributes
  PathToTemplateFile="$(IntermediateOutputPath)$(TargetName)$(TargetExt)"` reads Company,
  Copyright, FileVersion, InformationalVersion, Product, Title, Version from the compiled dll;
  then `WriteCodeFragment` writes `<culture>/<asm>.resources.cs` with them and `AssemblyCulture`
  (`Microsoft.NET.Sdk.targets`, the same in SDK 8.0.427 and 10.0.403). Under the record's
  `SkipCompilerExecution=true` the dll does not exist, so the captured source would lack them
  (or the target fails; not probed). Likely way out: capture the satellite's `Csc` switches only and
  derive the attributes from the main entry's `Generated` `*.AssemblyInfo.cs` (the same set, the
  version already a token) plus `AssemblyCulture`. Seen in a probe (SDK 8.0.427, `dotnet build
  -v:d`), the satellite's command line: `/noconfig /nostdlib+ /warn /reference:@(ReferencePath)
  /keyfile /optimize /out:obj/.../<culture>/<asm>.resources.dll
  /resource:<ns>.<name>.<culture>.resources /target:library /deterministic+ <cs>`: no `/define`,
  analyzers or `/debug`. This revisits decision 1 (capture vs compose) for the attributes only.
  **Probed** (`probe.md`, 2026-11-07): adding `CreateSatelliteAssemblies` to the record fails
  (MSB4018, no main dll), and the satellite's `Csc` takes neither `ProvideCommandLineArgs` nor
  `SkipCompilerExecution`, so it cannot be captured at all. Composing it from the derived source
  gives a byte-identical satellite for generated assembly info; hand-written attributes need
  the built main assembly's file version info.
- Signing parity, the version substitution (`VersionToken`), run-folder conflicts in culture
  subfolders, ICU vs NLS culture lists at the edges.
