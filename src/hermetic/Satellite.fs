namespace Xake.Hermetic.Dotnet

open System
open System.IO

open Xake.Dotnet

/// <summary>
/// A satellite assembly's compile by the recipe of the SDK's `CoreGenerateSatelliteAssemblies`,
/// which cannot be recorded (it reads the built assembly; `docs/features/satellites/probe.md`):
/// the switches it passes on from the main compile, the references it takes, the attributes it
/// reads back from the assembly, and the source `WriteCodeFragment` writes. The import records
/// what a project's own culture resources need (`Lock.Satellites`); the build derives the same
/// for an entry without them (`Locks.satelliteWith`). Pure, but `recipeOfSdk` and
/// `attributesFromAssembly`, which read a file.
/// </summary>
module Satellite =

    /// The attributes `GetAssemblyAttributes` hands a satellite assembly, in its order: what the
    /// SDK reads back from the built assembly's file version info (`docs/features/satellites/probe.md`).
    /// A copyright or a title the assembly does not state reads as " ".
    let private readBack =
        [ "System.Reflection.AssemblyCompanyAttribute", None
          "System.Reflection.AssemblyCopyrightAttribute", Some " "
          "System.Reflection.AssemblyDescriptionAttribute", None
          "System.Reflection.AssemblyFileVersionAttribute", None
          "System.Reflection.AssemblyInformationalVersionAttribute", None
          "System.Reflection.AssemblyProductAttribute", None
          "System.Reflection.AssemblyTitleAttribute", Some " "
          "System.Reflection.AssemblyVersionAttribute", None ]

    /// <summary>
    /// A satellite assembly's attribute lines (`Lock.Satellites.Attributes`) from the text of the
    /// assembly info msbuild generated for the main assembly (`[assembly: X("v")]` lines, as
    /// `WriteCodeFragment` writes them): those of `readBack`, in its order, the main
    /// assembly's `AssemblyConfiguration`, `NeutralResourcesLanguage`, `InternalsVisibleTo`, ...
    /// left out, as the SDK's target does. Pure.
    /// </summary>
    let attributesOf (assemblyInfo: string) : string list =
        let stated =
            [ for line in assemblyInfo.Split '\n' do
                let line = line.Trim ()
                if line.StartsWith "[assembly: " && line.EndsWith ")]" then
                    let body = line.Substring (11, line.Length - 12)
                    match body.IndexOf '(' with
                    | -1 -> ()
                    | i -> yield body.Substring (0, i), body ]
            |> Map.ofList
        [ for name, blank in readBack do
            match Map.tryFind name stated, blank with
            | Some body, _ when not (body.EndsWith "(\"\")") -> yield body
            | _, Some value -> yield sprintf "%s(\"%s\")" name value
            | _ -> () ]

    /// What the recording SDK's `CoreGenerateSatelliteAssemblies` hands csc besides the fixed
    /// switches, as it differs between SDKs: the references it filters `ReferencePath` to (by
    /// file name; `None`: all of them, SDK 8), whether it passes `PathMap` and `Features` (SDK 10).
    type SdkRecipe = { References: string list option; PathMap: bool; Features: bool }

    /// The recipe of SDK 8 (`SdkRecipe`): every reference, no path map, no features.
    let sdk8Recipe = { References = None; PathMap = false; Features = false }

    /// <summary>
    /// The `SdkRecipe` of the SDK whose `Microsoft.NET.Sdk.targets` is `targets`: read off
    /// its `CoreGenerateSatelliteAssemblies` target (the `_SatelliteAssemblyReferences` filter,
    /// the `Csc` task's `PathMap` and `Features`); SDK 8's when the target is not found. Pure.
    /// </summary>
    let recipeOf (targets: string) : SdkRecipe =
        match targets.IndexOf "<Target Name=\"CoreGenerateSatelliteAssemblies\"" with
        | -1 -> sdk8Recipe
        | start ->
            let block =
                match targets.IndexOf ("</Target>", start) with
                | -1 -> targets.Substring start
                | stop -> targets.Substring (start, stop - start)
            let filter = Text.RegularExpressions.Regex "<_SatelliteAssemblyReferences Include=\"@\\(ReferencePath\\)\" Condition=\"([^\"]*)\""
            let references =
                match filter.Match block with
                | m when m.Success && block.Contains "References=\"@(_SatelliteAssemblyReferences)\"" ->
                    Text.RegularExpressions.Regex.Matches (m.Groups.[1].Value, "'%\\(Filename\\)' == '([^']+)'")
                    |> Seq.cast<Text.RegularExpressions.Match> |> Seq.map (fun n -> n.Groups.[1].Value) |> List.ofSeq |> Some
                | _ -> None
            { References = references
              PathMap = block.Contains "PathMap=\"$(PathMap)\""
              Features = block.Contains "Features=\"$(Features)\"" }

    /// <summary>
    /// The switches of a satellite assembly's compile (`Lock.Satellites.Options`) by the recipe of
    /// the SDK's `CoreGenerateSatelliteAssemblies`: `/noconfig`, the main compile's switches the
    /// target passes on (the key, `/nostdlib`, `/warn`, `/optimize`, `/deterministic`,
    /// `/warnaserror` with a list; `/pathmap`, `/features` when the SDK's recipe passes them),
    /// `/nowarn` from `DisabledWarnings` (not the main compile's `NoWarn`), `/target:library`. Pure.
    /// </summary>
    let options (recipe: SdkRecipe) (mainOptions: string list) (disabledWarnings: string) : string list =
        let passed =
            set [ yield! [ "nostdlib"; "warn"; "keyfile"; "keycontainer"; "publicsign"; "delaysign"; "optimize"; "deterministic" ]
                  if recipe.PathMap then yield "pathmap"
                  if recipe.Features then yield "features" ]
        [ yield "/noconfig"
          for o in mainOptions do
              match CscArgs.parse o with
              | CscArgs.Switch (name, value) ->
                  let bare = name.TrimEnd ('+', '-')
                  if passed.Contains bare || (bare = "warnaserror" && value <> "") then yield o
              | CscArgs.Source _ -> ()
          if disabledWarnings <> "" then yield "/nowarn:" + disabledWarnings
          yield "/target:library" ]

    /// <summary>
    /// The attribute lines of a satellite assembly read from the built assembly at `path`, as the
    /// SDK's `GetAssemblyAttributes` reads them (its file version info and its version), for a
    /// project that writes its attributes itself (`Lock.Satellites.Attributes` = `None`).
    /// </summary>
    let attributesFromAssembly (path: string) : string list =
        let info = System.Diagnostics.FileVersionInfo.GetVersionInfo (Path.GetFullPath path)
        let version = System.Reflection.AssemblyName.GetAssemblyName(Path.GetFullPath path).Version
        let literal (value: string) =
            value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t")
        [ "System.Reflection.AssemblyCompanyAttribute", info.CompanyName
          "System.Reflection.AssemblyCopyrightAttribute", info.LegalCopyright
          "System.Reflection.AssemblyDescriptionAttribute", info.Comments
          "System.Reflection.AssemblyFileVersionAttribute", info.FileVersion
          "System.Reflection.AssemblyInformationalVersionAttribute", info.ProductVersion
          "System.Reflection.AssemblyProductAttribute", info.ProductName
          "System.Reflection.AssemblyTitleAttribute", info.FileDescription
          "System.Reflection.AssemblyVersionAttribute", (if isNull version then null else string version) ]
        |> List.choose (fun (name, value) -> if System.String.IsNullOrEmpty value then None else Some (sprintf "%s(\"%s\")" name (literal value)))

    /// The source of a satellite assembly of culture `culture` with the attribute lines
    /// `attributes`, as the SDK's `WriteCodeFragment` writes `<culture>/<asm>.resources.cs`. Pure.
    let source (attributes: string list) (culture: string) : string =
        let header =
            [ "//------------------------------------------------------------------------------"
              "// <auto-generated>"
              "//     This code was generated by a tool."
              "//"
              "//     Changes to this file may cause incorrect behavior and will be lost if"
              "//     the code is regenerated."
              "// </auto-generated>"
              "//------------------------------------------------------------------------------"
              ""
              "using System;"
              "using System.Reflection;"
              "" ]
        let lines = attributes @ [ sprintf "System.Reflection.AssemblyCultureAttribute(\"%s\")" culture ] |> List.map (sprintf "[assembly: %s]")
        header @ lines @ [ ""; "// Generated by the MSBuild WriteCodeFragment class."; "" ] |> String.concat "\n"

    /// <summary>
    /// The culture resources (culture, manifest name) no neutral resource of the main assembly
    /// matches -- `X.Strings.ja.resources` without `X.Strings.resources`: `ResourceManager` looks
    /// a culture's resources up by the neutral name, so nothing reads them. `mainNames` are the
    /// manifest names the main compile embeds. Pure.
    /// </summary>
    let orphans (mainNames: string list) (cultures: (string * Lock.SatelliteResource list) list) : (string * string) list =
        let neutral = set mainNames
        [ for culture, resources in cultures do
            for r in resources do
                let suffix = "." + culture + ".resources"
                let expected =
                    if r.Name.EndsWith (suffix, StringComparison.OrdinalIgnoreCase) then r.Name.Substring (0, r.Name.Length - suffix.Length) + ".resources"
                    else r.Name
                if not (neutral.Contains expected) then yield culture, r.Name ]

    /// The `SdkRecipe` of the SDK `version` under the .NET root `dotnetRoot`, read off its
    /// `Microsoft.NET.Sdk.targets`; `None` when that SDK is not installed there.
    let recipeOfSdk (dotnetRoot: string) (version: string) : SdkRecipe option =
        let targets = Path.Combine (dotnetRoot, "sdk", version, "Sdks", "Microsoft.NET.Sdk", "targets", "Microsoft.NET.Sdk.targets")
        if dotnetRoot <> "" && version <> "" && File.Exists targets then Some (recipeOf (File.ReadAllText targets)) else None
