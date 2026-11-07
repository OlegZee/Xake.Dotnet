# Release 4.2.0: lock-version-params

A public release, no preview: the consumer check (`consumer-check.md`, with the local
`4.2.0-version.1`) stands in for one. 4.2.0 carries this feature only;
`feature/import-file-casing` (#12) is not merged (the owner is not sure the fix belongs in the
import rather than in the source project). The tag publishes; a broken release is replaced by
4.2.1, never re-published.

## 1. The feature PR

```bash
git push -u origin feature/lock-version-params
```

Draft PR to `dev` with the prepared text; wait for `build`, `build-fsc`, `xake-latest`,
`windows`; mark ready, squash-merge. `dev` is at `f5294e0`, this branch's base: no sync needed
unless `dev` moves first.

## 2. The release commit (a PR to dev, as #10 for 4.1.0)

- `docs/release-notes.md`: `## 4.2.0 (unreleased)` -> `## 4.2.0 (<date>)`;
- `docs/session.md`, "Release state": `Xake.Dotnet` 4.2.0 (`docs/features/lock-version-params/`),
  the tag and its date;
- this tracker: the release item done.

## 3. Tag and publish

```bash
git checkout master && git pull && git merge --ff-only dev
```

```bash
git tag v4.2.0 && git push origin master --tags
```

`publish.yml` runs `build test pack` with `VERSION=4.2.0` and pushes `Xake.Dotnet 4.2.0`.

Checklist (`docs/devprocess.md`): `build test pack` and `build.fsc.fsx build check` green; the
nuspec has `FSharp.Core` 8.0.100 and `Xake [4.0.0, 5.0)`; release notes have the entry;
`samples/fullframework.fsx` runs after `build`.

## 4. After the publish

- nuget.org lists `Xake.Dotnet 4.2.0` (indexing takes minutes);
- the consumer script moves from `4.2.0-version.1` and its local `#i` source to
  `#r "nuget: Xake.Dotnet, 4.2.0"`; drop `~/.nuget/packages/xake.dotnet/4.2.0-version.1`;
- the preview `4.2.0-version.1` was never published: nothing to unlist.

## What it leaves for #12

If #12 lands later, it goes to 4.3.0 (its release notes say 4.2.0, and it changes the API:
`ResxFiles`, `Imported.Casing`). Merging `dev` into it then conflicts in three files; a dry run
(`git merge-tree` of this branch with #12 at `97f6044`, 2026-11-07):

- `src/hermetic/Import.fs`, four hunks, all additive: the import context record
  (`VersionMarker` next to `Casing` and `Roots`), the helpers after `depsTokens`, the context's
  construction in `record`, and the one place the features meet, an application's recorded
  `deps.json`. Its pipeline must be `depsTokens (scratch + appDeps) >> (match ctx.VersionMarker
  with Some m -> depsVersionTokens (scratch + appDeps) m | None -> id) >> depsFileNames renames`:
  versions first, then file names, so a miscased reference's `$(Version:lib.dll)` becomes
  `$(Version:Lib.dll)` with the other tokens. Neither feature tests that combination: add one.
- `src/hermetic.tests/RecordTests.fs`: both add fixtures at the end; keep both.
- `docs/release-notes.md`: #12's section becomes `## 4.3.0 (unreleased)` above 4.2.0.
