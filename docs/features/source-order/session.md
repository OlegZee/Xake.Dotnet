# Session: source-order

Branch `feature/source-order`, cut from `origin/dev` `b82100b` (4.4.0-preview.1 content).

## Where things stand (2026-11-19)

- Cause found and design written (`brief.md`); reported to the orchestrator; no code.
- B-4 (`feature/legacy-recipe`) was not merged into `origin/dev` yet at the time of the report.

## Next step

Wait for the owner's approval. Then: merge the latest `origin/dev` (with B-4; if B-4 is still not
merged, ask the orchestrator), implement `brief.md` "Design" 1-3, the tests, docs, `build test
pack`, the Windows VM check (probe projects: copy `ord1` style files -- 16 names as in `brief.md`).

## Probe recipe

- `dotnet msbuild -getItem:Compile` in a project with `Abc.cs, Abc/x.cs, Lower2.cs, lower/m.cs,
  _u.cs, ...`: macOS `/`-sorted, Windows (`ssh win11`) `\`-sorted.
- Options: `-t:<target depending on Build> -p:ProvideCommandLineArgs=true`, print `@(CscCommandLineArgs)`.
