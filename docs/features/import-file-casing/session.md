# Session: import-file-casing

Branch `feature/import-file-casing` (from `dev` at `68c786b`). Given by the owner (not re-opened):
the import records the repository's own spelling of each file, not the project file's, so that
descriptions and locks recorded on macOS or Windows build on Linux.

## State at 2026-11-04

Research done, no code changed. `brief.md` lists every place the import writes a path taken from
msbuild (description and lock), where the replay reads it and what fails on Linux, the recommended
resolver (git index for tracked files, a directory walk for the rest), the effect on existing locks
(no format bump; one re-import of the miscased projects), the tests, and six decisions for the owner.

Riskiest point: embedded resource names. msbuild derives them from the item's spelling, and
`ResourceManager` lookups are case-sensitive: correcting a resource's `file` must not change its name.

The owner answered all eight decisions (brief §7, 2026-11-04): the git index is the truth (read
with `git ls-files`, never parsed by us); only paths change, never a resource's name or a file's
content; every difference is warned at import; the replay is unchanged (an old miscased lock fails
as stale; the fix is a re-import); `ResXFileRef` files are recorded in the lock; doc `<include>`
references are warned only.

## Next step

Implementation, in the tracker's order, starting with the resolver.
