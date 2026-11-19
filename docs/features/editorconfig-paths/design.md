# Design: host paths in recorded analyzer configs (B-1)

Status: proposed 2026-11-19, waiting for the owner.

## Finding: the generator does not escape

`GenerateMSBuildEditorConfig` (Roslyn, `Microsoft.Build.Tasks.CodeAnalysis.dll`, wired in
`Roslyn/Microsoft.Managed.Core.targets`, target `GenerateMSBuildEditorConfigFileCore`) writes a
`build_property.*` / `build_metadata.*` value **verbatim**. Probed with one project whose
compiler-visible properties hold `C:\a\b\`, `\\srv\share\x`, `a\\\b`, `x"y`, a tab, `a#b;c`:

| SDK | host | `P1` | `P2` | `ProjectDir` | section header |
| --- | --- | --- | --- | --- | --- |
| 8.0.427 | macOS | `C:\a\b\` | `\\srv\share\x` | `/private/.../esc/` | `[/private/.../C.cs]` |
| 11.0.100 | macOS | same | same | same | same |
| 10.0.404 | Windows VM | `C:\a\b\` | `\\srv\share\x` | `C:\Users\...\escprobe\` | `[C:/Users/.../C.cs]` |

So on Windows the file holds `ProjectDir = C:\repo\src\Project\` with **single** backslashes
(section headers already use `/`). The doubled `C:\\repo\\...` the brief quotes is the lock's
**JSON** string escaping (`Json.escape`) of that single-backslash text. The brief's even-run
rule would never fire on the real file; the two faults are:

1. **Tokenization.** `Lock.format` tokenizes `Generated` with `Roots.tokenizeText`, which
   matches the root in its `/` spelling only (on purpose: in a `.cs` a `\` is a string escape).
   `C:\repo\...` never matches `C:/repo`.
2. **Host-string check.** `hostStrings` runs on the lock's JSON text; `slash` turns
   `C:\\repo\\src` into `C://repo//src`, which does not contain `C:/repo`. On the files beside a
   lock (raw text) the same check already works.

## Change (`src/hermetic/Import.fs` only)

1. `analyzerConfigPaths roots text` (pure, new, ~12 lines): for a recorded `.editorconfig` /
   `.globalconfig`, each `key = value` line whose value starts with a root in its `\` spelling
   (followed by `\` or the end) gets `\` -> `/` in that value, nothing else. Applied in
   `entryOf` where generated texts are read (`Import.fs:1541-1545`), before the scratch
   replacement, with the record's roots plus the scratch folder. Then the existing
   tokenization makes `$(Root)/repo/src/Project/`: the same text as a lock recorded on macOS.
   - Stored form: `/` only (the brief's "pick one"). At build `$(Root)` expands to
     `C:/repo/...`; the compiler and analyzers take `/` (as for every other lock path). The
     value differs from what msbuild writes on Windows by separators only.
   - No unescaping, no `//` collapsing: `\\srv\share\root\x\` with root `//srv/share/root`
     becomes `//srv/share/root/x/` -> `$(Root)/x/`, expanding back to `//srv/share/root/x/`.
   - A value under no root (`D:\m\n`), a `.cs`, a `.json`: unchanged. On macOS/Linux no value
     holds a root's `\` spelling, so every existing lock is byte-for-byte the same.
2. `hostStrings` (+2 lines): each line is also tested after undoing JSON's backslash escaping
   (`\\` -> `\`, left to right, then `slash`), so a Windows path left in a lock string is caught
   (`C:\\repo\\x` -> `C:/repo/x`; `\\\\srv\\share` -> `//srv/share`). The message is today's
   ("the lock ... would hold strings of this machine", with the line).

Root case: matching is ordinal as everywhere in tokenization today (a `ProjectDir` spelled in
another case than the root is not tokenized and the check then fails the import). Not changed.

## Tests (`src/hermetic.tests`, run on any OS, Windows strings as data)

- `analyzerConfigPaths`: `C:\root\repo\src\P\` with root `C:/root` -> `C:/root/repo/src/P/`, and
  through `Lock.format` -> `$(Root)/repo/src/P/`; UNC root keeps `//`; a value under no root,
  a `.cs` text and a `/`-only file unchanged; `C:\rootx\y` (prefix of a longer name) unchanged.
- `hostStrings`: a JSON line `"... = C:\\root\\x"` with root `C:/root` is reported; an
  escaped UNC root too; a tokenized line is not.
- No Windows record needed; optional end-to-end on `win11` after the code if the owner wants.

## Line budget

`Import.fs` ~+20, tests ~+60, docs (`docs/hermetic/lock.md` "Generated", release notes
"Unreleased") ~+10: about 90 lines.
