namespace Xake.Dotnet

open System
open System.IO
open System.Reflection.Metadata
open System.Reflection.PortableExecutable

open Xake

/// What a satellite resource whose neutral resource the main assembly lacks does to the build:
/// a warning (the default) or a failure.
[<RequireQualifiedAccess>]
type NeutralCheck =
    | Warn
    | Fail

/// <summary>
/// A satellite assembly's compile (`&lt;out&gt;/&lt;culture&gt;/&lt;Asm&gt;.resources.dll`) by the
/// recipe of SDK 10's `CoreGenerateSatelliteAssemblies`, on every SDK: the switches it passes on
/// from the main compile, the references it keeps, the attributes it reads back from the main
/// assembly (the template), the source `WriteCodeFragment` writes, the resource names, and the
/// checks that make the satellite load (culture, public key token, neutral names). The core of
/// `satellite { }` and of the hermetic `Locks.satellite`
/// (`docs/features/satellites/brief.md`, decisions 9-23). Pure, but `readTemplate`, `tokenOfFile`
/// and `resourcesOf`, which read files.
/// </summary>
module Satellite =

    /// One resource of a satellite: the file, the `.resources` a `.resx`/`.restext` compiles to
    /// ("" for a file embedded as is) and the manifest name.
    type Resource = { File: string; Compiled: string; Name: string }

    /// <summary>
    /// What a satellite takes from the main assembly, read from its metadata
    /// (`System.Reflection.Metadata`, the same on every OS): the name, the version, the public key
    /// (empty when unsigned), the target framework (`net8.0`, `net472`, `netstandard2.0`; `None`
    /// without a `TargetFrameworkAttribute`), the string assembly attributes by full type name, and
    /// the names of the embedded resources.
    /// </summary>
    type Template = {
        Path: string
        Name: string
        Version: Version
        PublicKey: byte[]
        Framework: string option
        Attributes: (string * string) list
        Resources: string list
    }

    /// The framework moniker of a `TargetFrameworkAttribute` value
    /// (`.NETCoreApp,Version=v8.0` -> `net8.0`, `.NETFramework,Version=v4.7.2` -> `net472`,
    /// `.NETStandard,Version=v2.0` -> `netstandard2.0`); `None` for anything else. Pure.
    let frameworkOf (attribute: string) : string option =
        match attribute.Split ',' |> List.ofArray with
        | identifier :: rest ->
            let version =
                rest |> List.tryPick (fun p ->
                    let p = p.Trim ()
                    if p.StartsWith "Version=v" then Some (p.Substring 9) else None)
            match identifier.Trim (), version with
            | ".NETCoreApp", Some v -> Some ("net" + v)
            | ".NETStandard", Some v -> Some ("netstandard" + v)
            | ".NETFramework", Some v -> Some ("net" + v.Replace (".", ""))
            | _ -> None
        | [] -> None

    /// The template of a satellite: the main assembly at `path`, read from its metadata.
    let readTemplate (path: string) : Template =
        let path = Path.GetFullPath path
        use stream = File.OpenRead path
        use pe = new PEReader (stream)
        if not pe.HasMetadata then failwithf "'%s' is not a .NET assembly (no metadata): a satellite's template is the main assembly" path
        let md = pe.GetMetadataReader ()
        let def = md.GetAssemblyDefinition ()
        let typeName (handle: EntityHandle) =
            let ofRef (h: TypeReferenceHandle) = let t = md.GetTypeReference h in md.GetString t.Namespace + "." + md.GetString t.Name
            let ofDef (h: TypeDefinitionHandle) = let t = md.GetTypeDefinition h in md.GetString t.Namespace + "." + md.GetString t.Name
            match handle.Kind with
            | HandleKind.MemberReference ->
                let m = md.GetMemberReference (MemberReferenceHandle.op_Explicit handle)
                match m.Parent.Kind with
                | HandleKind.TypeReference -> ofRef (TypeReferenceHandle.op_Explicit m.Parent)
                | HandleKind.TypeDefinition -> ofDef (TypeDefinitionHandle.op_Explicit m.Parent)
                | _ -> ""
            | HandleKind.MethodDefinition ->
                let m = md.GetMethodDefinition (MethodDefinitionHandle.op_Explicit handle)
                ofDef (m.GetDeclaringType ())
            | _ -> ""
        // the attributes with one string argument (`AssemblyCompanyAttribute("Co")`): the value
        // blob is the prolog 0x0001, then a SerString (0xFF for null)
        let attributes =
            [ for h in def.GetCustomAttributes () do
                let a = md.GetCustomAttribute h
                let name = typeName a.Constructor
                if name <> "" then
                    let mutable blob = md.GetBlobReader a.Value
                    if blob.Length >= 3 && blob.ReadUInt16 () = 1us then
                        let value = try blob.ReadSerializedString () with _ -> null
                        if not (isNull value) then yield name, value ]
        let resources = [ for h in md.ManifestResources -> md.GetString (md.GetManifestResource h).Name ]
        { Path = path
          Name = md.GetString def.Name
          Version = def.Version
          PublicKey = md.GetBlobBytes def.PublicKey
          Framework =
            attributes
            |> List.tryFind (fst >> (=) "System.Runtime.Versioning.TargetFrameworkAttribute")
            |> Option.bind (snd >> frameworkOf)
          Attributes = attributes
          Resources = resources }

    /// The public key token of a public key (the last 8 bytes of its SHA-1, reversed); empty for
    /// an empty key. Pure.
    let tokenOf (publicKey: byte[]) : byte[] =
        if Array.isEmpty publicKey then [||] else
        use sha = Security.Cryptography.SHA1.Create ()
        sha.ComputeHash publicKey |> Array.skip 12 |> Array.rev

    /// A token as hex (`4791180933e2efa9`; `null` when empty, as `AssemblyName` prints it). Pure.
    let tokenText (token: byte[]) : string =
        if Array.isEmpty token then "null" else token |> Array.map (sprintf "%02x") |> String.concat ""

    /// The public key token of the assembly at `path`, from its metadata.
    let tokenOfFile (path: string) : byte[] = tokenOf (readTemplate path).PublicKey

    let private reflection (name: string) = "System.Reflection." + name

    /// <summary>
    /// The full type name of an attribute a script names: `AssemblyCopyright`,
    /// `AssemblyCopyrightAttribute` and `System.Reflection.AssemblyCopyrightAttribute` are the
    /// same; a name with a namespace is kept. Pure.
    /// </summary>
    let attributeName (name: string) : string =
        let name = if name.EndsWith "Attribute" then name else name + "Attribute"
        if name.Contains "." then name else reflection name

    /// <summary>
    /// The attributes of a satellite of `template`, in the order `GetAssemblyAttributes` hands
    /// them (`probe.md`, D, F, G), as (full type name, value): Company, Description, FileVersion,
    /// Product when stated; Copyright and Title when stated, else `" "`; InformationalVersion when
    /// stated, else the assembly version; any of them stated as `""` left out; Version the assembly
    /// version. `explicit` (a script's, by `attributeName`) replaces the read one of that name, or
    /// comes after them. `AssemblyCulture` is `source`'s. Pure.
    /// </summary>
    let attributesOf (template: Template) (explicit: (string * string) list) : (string * string) list =
        let stated name = template.Attributes |> List.tryFind (fst >> (=) (reflection name)) |> Option.map snd
        let version = string template.Version
        let read =
            [ for name, absent in
                  [ "AssemblyCompanyAttribute", None
                    "AssemblyCopyrightAttribute", Some " "
                    "AssemblyDescriptionAttribute", None
                    "AssemblyFileVersionAttribute", None
                    "AssemblyInformationalVersionAttribute", Some version
                    "AssemblyProductAttribute", None
                    "AssemblyTitleAttribute", Some " " ] do
                match stated name, absent with
                | Some "", _ -> ()
                | Some value, _ -> yield reflection name, value
                | None, Some value -> yield reflection name, value
                | None, None -> ()
              yield reflection "AssemblyVersionAttribute", version ]
        let explicit = explicit |> List.map (fun (n, v) -> attributeName n, v)
        [ for name, value in read do
            match explicit |> List.tryFind (fst >> (=) name) with
            | Some (_, given) -> yield name, given
            | None -> yield name, value
          for name, value in explicit do
            if not (read |> List.exists (fst >> (=) name)) then yield name, value ]

    let private literal (value: string) =
        value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\000", "\\0")

    /// The source of a satellite of culture `culture` with `attributes` (`attributesOf`), as the
    /// SDK's `WriteCodeFragment` writes `<culture>/<asm>.resources.cs` (the compiled constants
    /// are the same; the text is not compared). Pure.
    let source (attributes: (string * string) list) (culture: string) : string =
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
        let lines =
            attributes @ [ reflection "AssemblyCultureAttribute", culture ]
            |> List.map (fun (name, value) -> sprintf "[assembly: %s(\"%s\")]" name (literal value))
        header @ lines @ [ ""; "// Generated by the MSBuild WriteCodeFragment class."; "" ] |> String.concat "\n"

    let private cultures =
        lazy (Globalization.CultureInfo.GetCultures Globalization.CultureTypes.AllCultures
              |> Array.map (fun c -> c.Name) |> Array.filter ((<>) "")
              |> fun names -> Collections.Generic.HashSet<string> (names, StringComparer.OrdinalIgnoreCase))

    /// Whether `name` is a culture's name .NET knows, in any case (`ja`, `zh-cn`). Pure.
    let isCulture (name: string) : bool = name <> "" && cultures.Value.Contains name

    /// <summary>
    /// Fails unless `culture` is a culture's name as .NET spells it (`zh-CN`, not `zh-cn`): the
    /// runtime probes `&lt;CultureInfo.Name&gt;/`, case-sensitively on Linux, and .NET Framework
    /// rejects a satellite whose `AssemblyCulture` differs. `what` names the satellite.
    /// </summary>
    let checkCulture (what: string) (culture: string) : unit =
        let spelled =
            if not (isCulture culture) then None
            else try Some (Globalization.CultureInfo.GetCultureInfo culture).Name with _ -> None
        match spelled with
        | Some name when name = culture -> ()
        | Some name -> failwithf "'%s': the culture folder '%s' is spelled '%s' by .NET: the runtime looks the satellite up there (case-sensitively on Linux)" what culture name
        | None -> failwithf "'%s': '%s' is not a culture .NET knows: a satellite's folder is its culture's name (ja, zh-CN)" what culture

    let private compiled (name: string) =
        name.EndsWith (".resx", StringComparison.OrdinalIgnoreCase) || name.EndsWith (".restext", StringComparison.OrdinalIgnoreCase)

    /// <summary>
    /// The manifest name in a satellite of `culture` of a file named `name` (`resourceset`'s name
    /// for it, the prefix in): the file's own culture suffix taken out (`Lib.Strings.ja.resx`,
    /// `Lib.Logo.ja.png`), then a `.resx`/`.restext` as `.&lt;culture&gt;.resources`
    /// (`Lib.Strings.ja.resources`), any other file by its name (`Lib.Logo.png`, as msbuild names
    /// a culture's non-resx file). A file whose suffix is another culture fails. Pure.
    /// </summary>
    let manifestName (culture: string) (name: string) : string =
        let ext = Path.GetExtension name
        let stem = name.Substring (0, name.Length - ext.Length)
        let suffix = Path.GetExtension stem
        let stem =
            if suffix.Length > 1 && isCulture (suffix.Substring 1) then
                if not (String.Equals (suffix.Substring 1, culture, StringComparison.OrdinalIgnoreCase)) then
                    failwithf "'%s' is a resource of the culture '%s', not of the satellite's '%s'" name (suffix.Substring 1) culture
                stem.Substring (0, stem.Length - suffix.Length)
            else stem
        if compiled name then sprintf "%s.%s.resources" stem culture else stem + ext

    /// <summary>
    /// The satellite resources of `culture` named by resource sets (`resourceset`, as `csc`'s
    /// `resources`): the files each set names, read with `getFiles` (a dependency of the rule: an
    /// added, removed or edited file rebuilds), named by the set (`prefix`, the folders below
    /// `basedir` when `dynamic`, the file) and `manifestName`. A `.resx`/`.restext` compiles to
    /// `&lt;compiledDir&gt;/&lt;name&gt;`.
    /// </summary>
    let resourcesOf (culture: string) (compiledDir: string) (sets: ResourceFileset list) : Recipe<ExecContext, Resource list> =
        sets
        |> List.fold (fun (acc: Recipe<ExecContext, Resource list>) (ResourceFileset (setOptions, (Fileset (fileOptions, _) as files))) ->
            recipe {
                let! before = acc
                let! (Filelist found) = getFiles files
                return
                    before
                    @ [ for f in found do
                          let name = manifestName culture (Impl.makeResourceName setOptions fileOptions.BaseDir f.FullName)
                          let file = f.FullName.Replace ('\\', '/')
                          yield { File = file; Compiled = (if compiled file then compiledDir.TrimEnd('/') + "/" + name else ""); Name = name } ] })
            (recipe { return [] })

    /// <summary>
    /// The manifest names of `resources` (a satellite's of `culture`) whose neutral resource
    /// `template` lacks: `X.&lt;culture&gt;.resources` needs `X.resources`, which is what
    /// `ResourceManager` looks a culture up by, so nothing would read it. Only `.resources` are
    /// checked: any other file is read by its own name. Pure.
    /// </summary>
    let neutralMisses (template: Template) (culture: string) (resources: Resource list) : string list =
        let suffix = "." + culture + ".resources"
        [ for r in resources do
            if r.Name.EndsWith (suffix, StringComparison.OrdinalIgnoreCase) then
                let neutral = r.Name.Substring (0, r.Name.Length - suffix.Length) + ".resources"
                if not (template.Resources |> List.contains neutral) then yield r.Name ]

    /// <summary>
    /// The resources of a satellite checked: none fails, a manifest name twice fails, a name
    /// whose neutral one the template lacks warns or fails (`check`). `what` names the satellite.
    /// </summary>
    let checkResources (what: string) (template: Template) (culture: string) (check: NeutralCheck) (resources: Resource list) : Recipe<ExecContext, unit> =
        recipe {
            if List.isEmpty resources then
                failwithf "'%s': no resources for the satellite of '%s'" what culture
            match resources |> List.countBy (fun r -> r.Name) |> List.filter (snd >> (<) 1) with
            | [] -> ()
            | twice -> failwithf "'%s': the satellite would embed %s twice" what (twice |> List.map fst |> String.concat ", ")
            match neutralMisses template culture resources with
            | [] -> ()
            | misses ->
                let message =
                    sprintf "'%s': %s %s no neutral resource of that name in '%s': ResourceManager never reads %s"
                        what (String.concat ", " misses) (if misses.Length = 1 then "has" else "have") (Path.GetFileName template.Path)
                        (if misses.Length = 1 then "it" else "them")
                match check with
                | NeutralCheck.Fail -> failwith message
                | NeutralCheck.Warn -> do! trace Level.Warning "%s" message
        }

    /// The references SDK 10's target keeps for a satellite: `mscorlib`, `netstandard`,
    /// `System.Runtime` among `references` (by file name, as msbuild compares: ignoring case).
    let keptReferences = [ "mscorlib"; "netstandard"; "System.Runtime" ]

    /// <summary>
    /// The references of a satellite among a compile's (`keptReferences`); none of them fails
    /// (`DisableImplicitFrameworkReferences`, a trimmed list): csc would fail on `System.Object`.
    /// `what` names the satellite. Pure.
    /// </summary>
    let references (what: string) (all: Reference list) : Reference list =
        match all |> List.filter (fun r -> keptReferences |> List.exists (fun k -> String.Equals (k, Path.GetFileNameWithoutExtension r.Path, StringComparison.OrdinalIgnoreCase))) with
        | [] ->
            failwithf "'%s': none of %s among the references: a satellite compiles against one of them" what (String.concat ", " keptReferences)
        | kept -> kept

    let private keySwitches = [ "keyfile"; "keycontainer"; "publicsign"; "delaysign" ]

    /// <summary>
    /// The switches of a satellite's compile but the references, resources, output and source,
    /// by SDK 10's recipe: `/noconfig`, the main compile's switches the target passes on (the key
    /// switches, `/nostdlib`, `/warn`, `/optimize`, `/deterministic`, `/warnaserror` with a list,
    /// `/pathmap`, `/features`), `/target:library`. No `/nowarn`: the target takes
    /// `DisabledWarnings`, which no SDK target sets. Pure.
    /// </summary>
    let options (mainOptions: string list) : string list =
        let passed = set (keySwitches @ [ "nostdlib"; "warn"; "optimize"; "deterministic"; "pathmap"; "features" ])
        [ yield "/noconfig"
          for o in mainOptions do
              match CscArgs.parse o with
              | CscArgs.Switch (name, value) ->
                  let bare = (CscArgs.canonical name).TrimEnd ('+', '-')
                  if passed.Contains bare || (bare = "warnaserror" && value <> "") then yield o
              | CscArgs.Source _ -> ()
          yield "/target:library" ]

    /// <summary>
    /// Fails before the compile when the key switches cannot give the template's token: a signed
    /// template without `/keyfile`/`/keycontainer`, or a key switch with an unsigned template.
    /// `what` names the satellite. Pure.
    /// </summary>
    let checkKey (what: string) (template: Template) (options: string list) : unit =
        let keyed =
            options |> List.exists (fun o ->
                match CscArgs.parse o with
                | CscArgs.Switch (name, _) -> let n = CscArgs.canonical name in n = "keyfile" || n = "keycontainer"
                | CscArgs.Source _ -> false)
        match Array.isEmpty template.PublicKey, keyed with
        | false, false ->
            failwithf "'%s': '%s' is signed (public key token %s) and the satellite has no key: give the key the assembly was signed with (keyfile; publicsign or delaysign as it was)"
                what (Path.GetFileName template.Path) (tokenText (tokenOf template.PublicKey))
        | true, true ->
            failwithf "'%s': '%s' is not signed, so its satellite must not be: drop the key" what (Path.GetFileName template.Path)
        | _ -> ()

    /// <summary>
    /// Fails when the satellite at `path` has another public key token than its template: .NET
    /// Framework and .NET would silently fall back to the neutral resources (`probe.md`, G).
    /// </summary>
    let checkToken (template: Template) (path: string) : unit =
        let expected = tokenOf template.PublicKey
        let built = tokenOfFile path
        if built <> expected then
            failwithf "'%s': the satellite's public key token is %s, its assembly's '%s' %s: it would never be loaded (another key?)"
                path (tokenText built) (Path.GetFileName template.Path) (tokenText expected)

    /// <summary>
    /// The satellite's compile: `options` (`options`, plus any of the script's), `references`,
    /// `/out:` the output, the resources (a compiled one by its `.resources`, the others as they
    /// are), the source at `sourcePath` with the text `sourceText` (`source`), generated before the
    /// compile. Name, framework, directory and compiler are the caller's to set. Pure.
    /// </summary>
    let compose (options: string list) (references: Reference list) (output: string) (sourcePath: string) (sourceText: string) (resources: Resource list) : Csc =
        let args =
            options
            @ [ for r in references -> "/reference:" + (if r.Alias = "" then r.Path else r.Alias + "=" + r.Path) ]
            @ [ "/out:" + output ]
            @ [ for r in resources -> sprintf "/resource:%s,%s" (if r.Compiled = "" then r.File else r.Compiled) r.Name ]
            @ [ sourcePath ]
        let c = Csc.ofArgs args
        { c with
            Generated = [ sourcePath, sourceText ]
            Resources = [ for r in resources do if r.Compiled <> "" then yield r.File, r.Compiled ]
            RuntimeConfig = None
            Dependencies = { c.Dependencies with References = references } }
