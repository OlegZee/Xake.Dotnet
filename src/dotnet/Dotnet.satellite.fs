namespace Xake.Dotnet

open System
open System.IO

open Xake

/// The settings of the `satellite {}` task (docs/tasks.md, "satellite"). Auto-opened, as
/// `CscSettingsType` is.
[<AutoOpen>]
module SatelliteTypes =

    /// <summary>
    /// A satellite assembly (`&lt;out&gt;/&lt;culture&gt;/&lt;Asm&gt;.resources.dll`) for a built
    /// main assembly, the template: the output is the rule's target and its folder the culture.
    /// </summary>
    type SatelliteSettingsType = {
        /// The main assembly (relative to the project root): version, attributes, public key,
        /// target framework and neutral resource names are read from its metadata. Required.
        Template: string
        /// The satellite's resources (`resourceset`), in order; a `.resx`/`.restext` is
        /// compiled, any other file embedded as is.
        Resources: ResourceFileset list
        /// The key the template was signed with (relative to the project root): `/keyfile:`.
        KeyFile: string option
        /// `/publicsign+`, as the main compile's
        PublicSign: bool
        /// `/delaysign+`, as the main compile's
        DelaySign: bool
        /// Assembly attributes replacing (or adding to) the ones read from the template, by
        /// name (`AssemblyCopyright`, `AssemblyFileVersionAttribute`, a full type name), in order.
        Attributes: (string * string) list
        /// What a resource without its neutral one in the template does (default `Warn`).
        NeutralCheck: NeutralCheck
        /// The target framework; the template's when null or empty, else checked against it.
        TargetFramework: string
        /// More compiler switches (`/pathmap:...`), kept as the main compile's switches are
        /// (`Satellite.options`); any other is left out with a warning.
        CommandArgs: string list
        /// Build fails on compile error.
        FailOnError: bool
    } with static member Default = {
            Template = ""
            Resources = []
            KeyFile = None
            PublicSign = false
            DelaySign = false
            Attributes = []
            NeutralCheck = NeutralCheck.Warn
            TargetFramework = null
            CommandArgs = []
            FailOnError = true
        }

/// Composing and running the compile of `satellite {}`, over the `Satellite` core.
module SatelliteTask =

    /// <summary>
    /// The switches a satellite compile takes besides `Satellite.options`'s own: those of SDK
    /// 10's `CoreGenerateSatelliteAssemblies` for a Release main compile (`/nostdlib+`,
    /// `/optimize+`, `/deterministic+`; `/warn` is left out, it does not change the bytes), then
    /// the key switches as csc's main compile has them (`/keyfile:&lt;abs&gt;`, `/publicsign+`;
    /// `/delaysign+` first), then `args`. A later switch of the same name wins with csc, so
    /// `args [ "/optimize-" ]` gives a Debug satellite.
    /// </summary>
    let internal mainOptions (root: string) (settings: SatelliteSettingsType) : string list =
        let keyfile = settings.KeyFile |> Option.map (fun k -> "/keyfile:" + Path.GetFullPath(Path.Combine (root, k)).Replace ('\\', '/')) |> Option.toList
        [ yield "/nostdlib+"
          if settings.DelaySign then yield "/delaysign+"
          yield! keyfile
          if settings.PublicSign then yield "/publicsign+"
          yield "/optimize+"
          yield "/deterministic+"
          yield! settings.CommandArgs ]

    let private normalFramework (fwk: string) = DotNetFwk.netcoreMoniker fwk |> Option.defaultValue fwk

    /// <summary>
    /// The target framework of a satellite: the template's (`TargetFrameworkAttribute`); a
    /// `targetfwk` that differs fails, and so does neither. `what` names the satellite. Pure.
    /// </summary>
    let frameworkOf (what: string) (template: Satellite.Template) (given: string) : string =
        let given = if String.IsNullOrWhiteSpace given then None else Some given
        match template.Framework, given with
        | Some t, Some g when not (String.Equals (normalFramework t, normalFramework g, StringComparison.OrdinalIgnoreCase)) ->
            failwithf "'%s': targetfwk '%s' differs from the framework of '%s' (%s): a satellite is built for its assembly's framework" what g (Path.GetFileName template.Path) t
        | Some t, _ -> t
        | None, Some g -> g
        | None, None ->
            failwithf "'%s': '%s' names no target framework (no TargetFrameworkAttribute): give it with targetfwk" what (Path.GetFileName template.Path)

    /// <summary>
    /// The satellite's compile, resolved without running it (and the template it is checked
    /// against): the output is the rule's target, the culture its folder (`Satellite.checkCulture`),
    /// named `&lt;template's name&gt;.resources.dll`; the template `need`ed and read; the
    /// framework (`frameworkOf`) found as `csc {}` finds one, its references filtered by
    /// `Satellite.references`; the key checked (`Satellite.checkKey`); the resources read
    /// (`Satellite.resourcesOf`, compiled under `obj/xake/satellites/&lt;out dir&gt;/&lt;culture&gt;/`)
    /// and checked (`Satellite.checkResources`); the attributes (`Satellite.attributesOf`) in the
    /// generated source beside them. Ends with `HermeticMode.enforce`, as `csc {}` does.
    /// </summary>
    let ofSettings (settings: SatelliteSettingsType) : Recipe<ExecContext, Csc * Satellite.Template * RunOptions> =
        recipe {
            let! options = getCtxOptions ()
            let root = Path.GetFullPath options.ProjectRoot
            let! target = getTargetFile ()
            let output = (File.getFullName target).Replace ('\\', '/')
            let what = (Impl.getRelative root (File.getFullName target)).Replace ('\\', '/')

            if String.IsNullOrWhiteSpace settings.Template then
                failwithf "'%s': satellite needs its template: template \"<the main assembly>\"" what
            let culture = Path.GetFileName (Path.GetDirectoryName (File.getFullName target))
            Satellite.checkCulture what culture

            let templatePath = Path.GetFullPath (Path.Combine (root, settings.Template))
            do! needFiles (Filelist [ File.make templatePath ])
            let template = Satellite.readTemplate templatePath
            let expectedName = template.Name + ".resources.dll"
            if Path.GetFileName output <> expectedName then
                failwithf "'%s': the satellite of '%s' is named '%s' (the runtime looks for that name)" what (Path.GetFileName templatePath) expectedName

            let framework = frameworkOf what template settings.TargetFramework
            let! _, fwkInfo = Csc.frameworkFor framework
            match DotNetFwk.sdkProbeWarning root with
            | Some warning -> do! trace Warning "%s" warning
            | None -> ()
            let! targetFwkInfo = DotNetFwk.resolveFramework (Some framework)
            let all =
                match DotNetFwk.frameworkReferences targetFwkInfo with
                | [] -> [ DotNetFwk.locateAssembly targetFwkInfo "mscorlib.dll" ]
                | refs -> refs
            let references = Satellite.references what [ for path in all -> { Path = path; Sha256 = ""; Alias = "" } ]

            let main = mainOptions root settings
            let switches = Satellite.options main
            for arg in settings.CommandArgs do
                if not (List.contains arg switches) then
                    do! trace Warning "'%s': the satellite's compile leaves out '%s' (only the switches the SDK passes on from the main compile are kept)" what arg
            Satellite.checkKey what template switches

            let outDir =
                let rel = Impl.getRelative root (Path.GetDirectoryName (Path.GetDirectoryName (File.getFullName target)))
                if Path.IsPathRooted rel then rel.Replace(":", "").TrimStart ('/', '\\') else rel
            let compiledDir = (Path.Combine (root, "obj", "xake", "satellites", outDir, culture)).Replace ('\\', '/')
            let! resources = Satellite.resourcesOf culture compiledDir settings.Resources
            do! Satellite.checkResources what template culture settings.NeutralCheck resources

            let attributes = Satellite.attributesOf template settings.Attributes
            let sourcePath = compiledDir + "/" + template.Name + ".resources.cs"
            let c = Satellite.compose switches references output sourcePath (Satellite.source attributes culture) resources
            let compilerPath = Csc.managedCompiler fwkInfo.CscTool
            let c =
                { c with
                    Name = template.Name + ".resources"
                    Framework = framework
                    Directory = root
                    Dependencies =
                        { c.Dependencies with
                            Compiler = { Tool = "csc"; Path = compilerPath; Sha256 = ""; Version = Csc.compilerVersion compilerPath } } }
            do! HermeticMode.enforce c.Name ({ HermeticMode.Role = HermeticMode.Other; HermeticMode.Path = templatePath } :: Csc.hermeticInputs c)

            let! server = CompilerServer.resolve None
            let run = { RunOptions.Default with FailOnError = settings.FailOnError; Server = server; Environment = fwkInfo.EnvVars }
            return c, template, run
        }

    /// <summary>
    /// The `satellite {}` task: `ofSettings`, `Csc.run`, then `Satellite.checkToken` on the
    /// output (another public key token than the template's would never be loaded).
    /// </summary>
    let compile (settings: SatelliteSettingsType) : Recipe<ExecContext, unit> =
        recipe {
            let! c, template, run = ofSettings settings
            do! Csc.run run c
            match c.Output with
            | Some output when File.Exists output -> Satellite.checkToken template output
            | _ -> ()
        }

/// The `satellite {}` builder; the task is `SatelliteTask.compile`.
[<AutoOpen>]
module SatelliteBuilder =

    /// Computation expression builder for the satellite task.
    type SatelliteSettingsBuilder() =

        /// <summary>The main assembly (required).</summary>
        [<CustomOperation("template")>]     member __.Template(s: SatelliteSettingsType, path: string) = { s with Template = path }
        /// <summary>A resource set (`resourceset`); accumulates.</summary>
        [<CustomOperation("resources")>]    member __.Resources(s: SatelliteSettingsType, set: ResourceFileset) = { s with SatelliteSettingsType.Resources = s.Resources @ [ set ] }
        /// <summary>The key file the template was signed with.</summary>
        [<CustomOperation("keyfile")>]      member __.KeyFile(s: SatelliteSettingsType, path: string) = { s with KeyFile = Some path }
        /// <summary>`/publicsign+`, as the main compile's.</summary>
        [<CustomOperation("publicsign")>]   member __.PublicSign(s: SatelliteSettingsType) = { s with PublicSign = true }
        /// <summary>`/delaysign+`, as the main compile's.</summary>
        [<CustomOperation("delaysign")>]    member __.DelaySign(s: SatelliteSettingsType) = { s with DelaySign = true }
        /// <summary>An assembly attribute replacing (or adding to) the one read from the
        /// template; accumulates.</summary>
        [<CustomOperation("attribute")>]    member __.Attribute(s: SatelliteSettingsType, name: string, value: string) = { s with Attributes = s.Attributes @ [ name, value ] }
        /// <summary>What a resource without its neutral one does: `NeutralCheck.Fail` fails
        /// (default `Warn`).</summary>
        [<CustomOperation("neutralcheck")>] member __.NeutralCheck(s: SatelliteSettingsType, check: NeutralCheck) = { s with NeutralCheck = check }
        /// <summary>The target framework, checked against the template's.</summary>
        [<CustomOperation("targetfwk")>]    member __.TargetFwk(s: SatelliteSettingsType, fwk: string) = { s with TargetFramework = fwk }
        /// <summary>More compiler switches (`/pathmap:...`).</summary>
        [<CustomOperation("args")>]         member __.Args(s: SatelliteSettingsType, args: string list) = { s with CommandArgs = args }
        /// <summary>Whether a compile error fails the build (default true).</summary>
        [<CustomOperation("failonerror")>]  member __.FailOnError(s: SatelliteSettingsType, value: bool) = { s with SatelliteSettingsType.FailOnError = value }

        member __.Bind(x, f) = f x
        member __.Yield(()) = SatelliteSettingsType.Default
        member __.For(x, f) = f x
        member __.Zero() = SatelliteSettingsType.Default

        member __.Run(s: SatelliteSettingsType) = SatelliteTask.compile s

    /// The satellite task builder instance.
    let satellite = SatelliteSettingsBuilder()
