#r "nuget: Xake, 4.0.0"
#load "testjobs.fsx"

// Builds the one package of this repository, `Xake.Dotnet`: the assemblies Xake.Dotnet
// (src/dotnet) and Xake.Hermetic.Dotnet (src/hermetic) at one version, on the published `Xake`
// package (the range in src/dotnet/Xake.Dotnet.fsproj, `XakeVersion`).
//
// `test` runs the test jobs of testjobs.fsx in parallel (`test::dotnet/jobs`, `test::hermetic/run`,
// ...; one runs alone by naming it). The test projects are built once (`test-build::<project>`),
// each job runs `dotnet test --no-build` on its slice and writes into out/test/<project>/<slice>/
// (the trx, the blame sequence file and console.log, its whole output); the console gets the
// summary lines. FILTER (-d FILTER=Rm) narrows every job to its tests whose name matches Rm
// (NUnit's `name =~ 'Rm'`, a regex; no quote in it); a job left with no test passes.
// XAKE_VERSION (-d XAKE_VERSION=4.*-*) builds and tests against another `Xake` than the range
// of the project file (CI's xake-latest job).

open Xake
open Xake.Tasks
open Xake.Typed
open Testjobs

let vars = {|
    // The names are explicit: publish.yml exports VERSION, and `-d VERSION=` is the same name; a
    // field without `cliArg` takes `-d <Key>=`, without `envVar` it reads XAKE__<NAME>.
    Version     = Var.create<string>(cliArg = "VERSION", envVar = "VERSION", description = "Version number for the Xake.Dotnet package, e.g. 4.0.1") |> withDefault "0.0.1"
    NUGET_KEY   = Var.env<string>(envVar = "NUGET_KEY", description = "API key for NuGet.org, required for pushing the package") |> withDefault "" |> secret
    TestFilter  = Var.string(cliArg = "FILTER", envVar = "FILTER", description = "Runs only the tests whose name matches this regex, e.g. 'Rm'")
    XakeVersion = Var.string(cliArg = "XAKE_VERSION", envVar = "XAKE_VERSION", description = "The Xake package version (or range, e.g. 4.*-*) to build and test against instead of the project file's")
|}

let frameworks = ["netstandard2.0" (*; "net462" *)]

/// A library this script builds. `Needs` are the libraries (of this script) it is compiled against.
type Library = { Name: string; Dir: string; Needs: string list }

let libraries =
    [ { Name = "Xake.Dotnet";          Dir = "src/dotnet";   Needs = [] }
      { Name = "Xake.Hermetic.Dotnet"; Dir = "src/hermetic"; Needs = ["Xake.Dotnet"] } ]

let library name = libraries |> List.find (fun lib -> lib.Name = name)

/// Assembly and doc file of a library, per target framework.
type LibG = { fwk: string; lib: string }
let Lib = Target<LibG> ["out/(fwk:*)/(lib:*).dll"; "out/(fwk:*)/(lib:*).xml"]

/// The package: packed from src/dotnet, which adds the hermetic assembly to the same nupkg (see
/// src/dotnet/Xake.Dotnet.fsproj, IncludeHermeticOutput).
let Package = Target<string> "out/pkg/Xake.Dotnet.(ver:*).nupkg"
let TestProject = Command<string> "test-project::(project:*)"

/// What the nuspec and the lib folders of the package must hold; returns the problems found (none
/// means the package is right): a dependency on `Xake`, none on `Xake.Hermetic.Dotnet` (not a
/// package any more), both assemblies with their docs in lib/net462 and lib/netstandard2.0, and
/// no Xake.dll.
let checkPackage (nupkg: string) =
    use zip = System.IO.Compression.ZipFile.OpenRead nupkg
    let names = zip.Entries |> Seq.map (fun e -> e.FullName) |> Set.ofSeq
    let entry = zip.Entries |> Seq.find (fun e -> e.FullName.EndsWith ".nuspec" && not (e.FullName.Contains "/"))
    use reader = new System.IO.StreamReader(entry.Open())
    let doc = System.Xml.Linq.XDocument.Parse(reader.ReadToEnd())
    let ids =
        doc.Descendants()
        |> Seq.filter (fun e -> e.Name.LocalName = "dependency")
        |> Seq.map (fun e -> e.Attribute(System.Xml.Linq.XName.Get "id").Value)
        |> List.ofSeq
    [ if not (List.contains "Xake" ids) then yield "no dependency on package Xake"
      if List.contains "Xake.Hermetic.Dotnet" ids then yield "depends on Xake.Hermetic.Dotnet, which ships inside this package"
      for tfm in ["net462"; "netstandard2.0"] do
        for asm in ["Xake.Dotnet"; "Xake.Hermetic.Dotnet"] do
          for ext in ["dll"; "xml"] do
            let f = $"lib/{tfm}/{asm}.{ext}"
            if not (names.Contains f) then yield $"{f} is missing"
      for n in names do
        if n.EndsWith "/Xake.dll" then yield $"{n}: the engine ships in package Xake" ]

/// `-p:XakeVersion=...` when XAKE_VERSION is set.
let xakeVersionArgs = recipe {
    let! v = vars.XakeVersion
    return [ for v in Option.toList v -> $"-p:XakeVersion={v}" ]
}

let dotnet arglist = sh "dotnet" { args arglist; failonerror }

do xakeScript {
    filelog "build.log" Diag
    varschema vars

    // a test running longer than 10 minutes is a hang: the run is stopped and the
    // test named (the sequence file in the job's folder) instead of waiting forever
    rules (testRules xakeVersionArgs [ "--blame-hang"; "--blame-hang-timeout"; "10m"; "--blame-hang-dump-type"; "none" ] vars.TestFilter)
    rules [
        // all jobs of one test project, e.g. `test-project::hermetic`
        TestProject.Rule <| fun project -> recipe {
            do! need [ for j in testJobs do if j.Project = project then testJobTarget j ]
        }
        "main" <<< ["build"; "test"]

        "build" <== [ for lib in libraries do yield! Lib.For [ for fwk in frameworks -> { fwk = fwk; lib = lib.Name } ] ]
        "clean" => rm {dir "out"}

        // one rule compiles them all: which library and which framework is asked for
        // is read off the target being built
        Lib.Rule <| fun m -> recipe {
            let framework = m.fwk
            let lib = library m.lib

            let! allFiles = getFiles <| fileset {
                basedir lib.Dir
                includes $"%s{lib.Name}.fsproj"
                includes "**/*.fs"
            }

            do! needFiles allFiles
            do! need (Lib.For [ for dep in lib.Needs -> { m with lib = dep } ])
            let! version = vars.Version
            let! xv = xakeVersionArgs

            do! dotnet ([
                "build"
                lib.Dir
                "--configuration"; "Release"
                "--framework"; framework
                "--output"; "./out/" + framework
                "/p:Version=" + version
              ] @ xv)
        }

        (* Nuget publishing rules *)
        command "pack" {
            let! version = vars.Version
            do! need [Package.Format version]
        }

        Package.Rule <| fun ver -> recipe {
            let! nupkg = getTargetFullName()
            do! dotnet [
                "pack"; "src/dotnet"
                "-c"; "Release"
                $"/p:Version={ver}"
                "--output"; "out/pkg/"
            ]

            match checkPackage nupkg with
            | [] -> do! trace Level.Info "%s: depends on Xake, carries Xake.Dotnet and Xake.Hermetic.Dotnet" (System.IO.Path.GetFileName nupkg)
            | problems ->
                System.IO.File.Delete nupkg
                failwithf "%s: %s" nupkg (String.concat "; " problems)
        }

        // push needs pack to be explicitly called in advance
        command "push" {
            let! version = vars.Version
            let! nuget_key = vars.NUGET_KEY
            do! dotnet [
                "nuget"; "push"; Package.Format version
                "--source"; "https://www.nuget.org/api/v2/package"
                "--api-key"; nuget_key
            ]
        }
    ]
}
