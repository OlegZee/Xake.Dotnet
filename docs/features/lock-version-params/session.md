# Session: lock-version-params

Branch `feature/lock-version-params` from `dev` at `f5294e0`. Goal and design: `brief.md`
(the brief came from a consumer; "Decisions taken" at its end); items: `tracker.md`.

## State at 2026-11-07: released

- Released in `Xake.Dotnet` 4.2.0 (PR #13, tag `v4.2.0` on `22d61e0`, nuget.org 2026-11-07).
  API: `Import.RecordOptions.VersionToken`, `Locks.Settings.VersionSuffixes`, `Locks.versionOf`,
  `Locks.suffixProblem`; how-to `docs/hermetic/versioning.md`.
- The consumer check passed (`consumer-check.md`); the consumer's own setup (release and dev
  builds, its docs, a PR) is the consumer session's.
- `release.md` lists, for `feature/import-file-casing` (#12), the conflicts it meets in
  `Import.fs` if it lands later (as 4.3.0), the `deps.json` pipeline order among them.

## Next step

Nothing open for 4.2.0. The "Later" items of `tracker.md` start only when asked for.
