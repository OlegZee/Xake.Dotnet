# Session: source-order

**Released in 4.4.0 (2026-11-19).** Only the "Later" items of `tracker.md` are open.

Branch `feature/source-order`, cut from `origin/dev` `b82100b` (4.4.0-preview.1 content).

## Where things stand (2026-11-19)

- Implemented as approved (`brief.md`), committed locally, NOT pushed (the owner's go in this session is needed).
- `build test pack` green; Windows VM acceptance passed (`windows/README.md`); repository locks unchanged.
- B-4 merged in from `origin/dev` (`2fd8dc5`).

## Next step

PR #22 open (pushed 2026-11-19): wait for CI (incl. `build-fsc`), then squash-merge into `dev`. Then the
consumer re-imports on both OSes with the next preview and compares (also confirms the `Options` cause).

## Probe recipe

- `dotnet msbuild -getItem:Compile` in a project with `Abc.cs, Abc/x.cs, Lower2.cs, lower/m.cs,
  _u.cs, ...`: macOS `/`-sorted, Windows (`ssh win11`) `\`-sorted.
- Options: `-t:<target depending on Build> -p:ProvideCommandLineArgs=true`, print `@(CscCommandLineArgs)`.
