# Brief: escaped paths in generated `.editorconfig` / `.globalconfig` (Xake.Dotnet, after 4.3.0)

Audience: the agent working in the Xake.Dotnet repository. Xake.Dotnet is public: this brief names no product or
company of the consumer. Code references are to v4.3.0 (`src/hermetic`, commit `b4853da`).

## Problem

A lock of an SDK project recorded on Windows keeps the generated `GeneratedMSBuildEditorConfig.editorconfig` with a
host path in it:

```
build_property.ProjectDir = C:\\vsx\\meta\\repo\\src\\Project\\
```

The value is **escaped**: every backslash of the real path `C:\vsx\meta\repo\src\Project\` is written twice. Two
consequences:

1. Tokenization does not replace the root with the configured root token (`$(Root)`), so a
   lock recorded on Windows differs from the same lock recorded on macOS/Linux and carries the import machine's
   path.
2. The check "no host strings in the lock" misses it: the leak is silent.

No compile output changes (analyzers in use do not read `ProjectDir`), but locks stop being portable and the
host-string check gives a false sense of safety.

**Where.** `Import.fs:1825`, `hostStrings`: the text goes through `slash` (`\` → `/`). The escaped `C:\\vsx` becomes
`C://vsx//`, which does not contain the candidate root `C:/vsx`. Tokenization most likely has the same flaw.

## What to do

Treat the value as what it is, an escaped string, and unescape it **exactly** before any path handling. No
heuristic slash collapsing: collapsing `//` breaks UNC roots (`\\server\share` → `//server/share` is correct,
`/server/share` is a different path).

1. **Scope.** Only the values (right-hand side of `key = value`) of generated `*.editorconfig` and `*.globalconfig`
   files the lock stores (`Compilation.Generated` and analyzer config inputs). Other generated text keeps its
   current handling.
2. **Detect escaping.** A value is treated as escaped when every maximal run of `\` in it has even length.
   - Even runs only → escaped: replace every `\\` with `\` (halve each run, strictly, left to right). `C:\\vsx\\` →
     `C:\vsx\`; `\\\\server\\share` → `\\server\share`.
   - A run of odd length → not escaped (or escaping we do not understand): leave the value unchanged and, if it
     contains a host string after the normal check, fail the import as today (see 4). Do not guess.
   - No backslashes → nothing to do.
   If the SDK's generator documents its escaping (check `GenerateMSBuildEditorConfig` in the SDK sources used by
   import), follow that rule instead and note the source in a comment; the even-run rule is the fallback.
3. **Then normalize** the unescaped path with the same function used for all other lock paths (`\` → `/`, no
   collapsing), compare with the normalized root, replace the root with the token, and **write the value back
   escaped the way it was found** (re-double the backslashes if the token expansion yields backslashes on Windows;
   if the stored lock form is `/`-only, keep it `/`-only — pick one and document it). At build time the token
   expands and the value is re-escaped consistently, so the file csc sees is equivalent to what msbuild produced.
4. **Host-string check** runs on the unescaped value too, so an escaped leak now fails the import with the key, the
   file and the offending value.

## Acceptance

- Unit tests for the unescape step: `C:\\a\\b\\` → `C:\a\b\`; `\\\\srv\\share\\x` → `\\srv\share\x`; `a\b` (odd run)
  unchanged; `a\\\b` (run of 3) unchanged; no backslashes unchanged.
- Unit test: an editorconfig with `build_property.ProjectDir = C:\\root\\repo\\src\\P\\` and root `C:\root` becomes
  `$(Root)/repo/src/P/` (in the chosen stored form).
- Unit test with a UNC root: `\\\\srv\\share\\root\\repo\\` and root `\\srv\share\root` tokenizes; the result keeps
  the double leading separator when expanded.
- A host path left in an escaped value fails the import with a clear message.
- A project imported on Windows produces the same analyzer config text in its lock as on macOS (modulo nothing).
- Locks without escaped values are byte-for-byte unchanged.

UNC roots are not an officially supported setup; this change must just not make them worse.
