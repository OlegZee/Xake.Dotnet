# Brief: non-SDK (legacy) projects — document the import overlay recipe, fail clearly (Xake.Dotnet, after 4.3.0)

Audience: the agent working in the Xake.Dotnet repository. Xake.Dotnet is public: this brief names no product or
company of the consumer. Code references are to v4.3.0 (`src/hermetic`, commit `b4853da`).

Decision: Xake.Dotnet does **not** support legacy (non-SDK) csproj natively. It documents how a consumer makes such a
project importable with an import overlay (a `.targets` file passed through `CustomAfterMicrosoftCommonTargets`), and
the import fails with a clear message when the overlay is missing.

## Background

A legacy csproj (`<Project ToolsVersion=…>` without `Sdk=`, `TargetFrameworkVersion` instead of `TargetFramework`)
evaluated by .NET MSBuild lacks what import relies on. The consumer has two such projects (Visual Studio
extensions) and imports them on .NET MSBuild with an overlay that does four things:

| Section | What | Why |
|---|---|---|
| A. Framework | `TargetFramework` from `TargetFrameworkIdentifier` + `TargetFrameworkVersion` (`.NETFramework`, `v4.7.2` → `net472`) | import keys entries by TFM and finds none |
| B. Packages | import `Microsoft.PackageDependencyResolution.targets` from the SDK (plus the restore targets) when `Microsoft.NuGet.targets` is absent | `Microsoft.NuGet.targets` ships only with Visual Studio's MSBuild; without it `PackageReference` assets are never resolved and references are missing |
| C. XAML | import `Microsoft.WinFX.targets` for `Page` items | the XAML markup compiler is not wired into legacy projects under .NET MSBuild |
| D. Reference assemblies | `Microsoft.NETFramework.ReferenceAssemblies` package | none installed on macOS/Linux (see also the reference-assemblies brief) |

The consumer's overlay is a working example; this brief asks to turn it into documentation and a guard.

## What to do

1. **Docs page** `docs/legacy-projects.md` (or a section of the import docs):
   - what "legacy" means for import and why it is not supported natively;
   - the overlay recipe, sections A–D, as a minimal self-contained `.targets` with comments: properties, which SDK
     files to import and the condition that keeps it away from SDK projects (`'$(UsingMicrosoftNETSdk)' != 'true'`);
   - how to pass it (`CustomAfterMicrosoftCommonTargets`, and that the overlay is an import input, so its edit makes
     locks stale);
   - known limits: only what the recipe covers (C# library/app, PackageReference, XAML `Page`); `packages.config`,
     VB, C++/CLI, COM references are out.
2. **Guard in import.** Detect a non-SDK project (no `UsingMicrosoftNETSdk=true` after evaluation). If it also has no
   `TargetFramework`, fail the import of that project with:
   `<project>: legacy (non-SDK) project; import needs TargetFramework and PackageReference resolution — see docs/legacy-projects.md`.
   If the overlay supplied what is needed, proceed silently. Today such a project fails later with an unrelated
   error or is skipped without a word (verify which and mention it in the docs).
3. **Test fixture**: a tiny legacy project (one `.cs`, one `PackageReference`, one XAML `Page` on Windows only) with
   the documented overlay; the import test records it and the lock builds. Without the overlay the test expects the
   guard message.

## Acceptance

- The docs recipe, copied as is, makes the fixture importable on .NET MSBuild (macOS/Linux for A, B, D; Windows for C).
- Without the overlay the import fails with the guard message, not a downstream error.
- SDK projects: no behaviour change, the guard never fires.

## Decisions (owner, 2026-11-19, after the probe)

- **The guard checks A, B and D**, not only `TargetFramework`: a non-SDK project fails when it has no
  `TargetFramework(s)` (A), when the SDK's package resolution is not imported (`NETCoreSdkVersion` or
  `ProjectAssetsCacheFile` empty, B), or when it targets .NET Framework, the import wants the reference
  assemblies package (`AutomaticallyUseReferenceAssemblyPackages=true`) and it has no `PackageReference`
  to `Microsoft.NETFramework.ReferenceAssemblies` (D). The message is the one above with a suffix naming
  what is missing: `(missing: A framework, B package resolution, D reference assemblies)`. C is not checked.
- **Section C stays in the same overlay file**, with a note to drop it where no project has XAML (on a
  project without XAML it only adds `Version` 1.0.0.0 to the lock's properties). The probe found that C
  works on macOS too, not only on Windows.
- Section D is still needed after B-5: the SDK adds the package by a target a legacy project does not
  import. In the overlay it is one item reading B-5's globals (version, switch).
