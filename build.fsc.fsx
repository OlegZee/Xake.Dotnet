// Declarative self-build (docs/hermetic/lock.md): what a project is lives in src/<dir>/xake.project.json,
// how it compiles in locks/<name>/Release.json; both committed, written only by `import` and `update-locks`.
// `build` compiles from them with fsc, no msbuild; `check` fails where `build` only warns (xake.fingerprints).
// Bootstrap: the published packages (the exact published version, bumped only after a release lands on nuget.org).
#r "nuget: Xake, 4.0.0"
#r "nuget: Xake.Dotnet, 4.0.0-preview.1"   // both assemblies: Xake.Dotnet and Xake.Hermetic.Dotnet
#load "testjobs.fsx"   // the test jobs, shared with build.fsx

open Xake
open Xake.Tasks
open Xake.Typed
open Xake.Dotnet
open Xake.Hermetic.Dotnet
open Testjobs

let vars = {|
    Version    = Var.create<string>(description = "Xake.Dotnet package version (both assemblies)") |> withDefault "0.0.1"
    TestFilter = Var.string(cliArg = "FILTER", envVar = "FILTER", description = "Runs only the tests whose name matches this regex, e.g. 'Lock'")
    XakeFloor  = Var.string(cliArg = "XAKE_FLOOR", description = "bump-base: the Xake version to build against (default: the newest on nuget.org)")
|}

// The engine is the published `Xake` package (a package reference in the descriptions), not a
// project here. The paths come from the patterns; the functions the Locks API takes are made of them.
type ProjG = { dir: string; name: string }
let Fsproj = Target<ProjG> "src/(dir:*)/(name:*).fsproj"
let Desc = Target<string> "src/(dir:*)/xake.project.json"   // beside the project file
type LockG = { name: string; cfg: string }
let Lock = Command<LockG> "lock::(name:*)::(cfg:*)"
let LockFile = Target<LockG> "locks/(name:*)/(cfg:*).json"
type DllG = { cfg: string; fwk: string; name: string }
let Dll = Target<DllG> "out/(cfg:*)/(fwk:*)/(name:*).dll"
let projects = [ { dir = "dotnet"; name = "Xake.Dotnet" }; { dir = "hermetic"; name = "Xake.Hermetic.Dotnet" } ]
let descriptionOf name = Desc.Format (projects |> List.find (fun p -> p.name = name)).dir
let lockOf name cfg = LockFile.Format { name = name; cfg = cfg }
let outOf cfg fwk name = System.IO.Path.GetDirectoryName (Dll.Format { cfg = cfg; fwk = fwk; name = name })
let versions = [ ("**", vars.Version) ]   // one package, one version
let fingerprints = "xake.fingerprints"

/// bump-base's view of the engine version: the versions nuget.org lists and the floor of the
/// `XakeVersion` range in src/dotnet/Xake.Dotnet.fsproj (the one place it is written).
module XakeFloor =
    let projectFile = "src/dotnet/Xake.Dotnet.fsproj"
    let private rangeRx =
        System.Text.RegularExpressions.Regex @"(<XakeVersion Condition=""'\$\(XakeVersion\)' == ''"">\[)([^,\]]+)(,[^<]*</XakeVersion>)"

    /// The Xake versions on nuget.org, oldest first (the flat-container index, previews included).
    let published () =
        use http = new System.Net.Http.HttpClient ()
        let json = http.GetStringAsync("https://api.nuget.org/v3-flatcontainer/xake/index.json").Result
        use doc = System.Text.Json.JsonDocument.Parse json
        [ for v in doc.RootElement.GetProperty("versions").EnumerateArray () -> v.GetString () ]

    /// The default floor: the newest 4.x (or later) release, else the newest 4.x preview.
    let newest (published: string list) =
        let major (v: string) = int (v.Split('.').[0])
        let current = published |> List.filter (fun v -> major v >= 4)
        match current |> List.filter (fun v -> not (v.Contains "-")) with
        | [] when current = [] -> failwith "bump-base: no Xake 4.x on nuget.org"
        | [] -> List.last current
        | releases -> List.last releases

    /// Writes `floor` as the lower bound of the range; the upper bound stays. Returns the floor
    /// it replaced. A UTF-8 BOM the file starts with is kept (`ReadAllText` drops it).
    let write (floor: string) =
        let bytes = System.IO.File.ReadAllBytes projectFile
        let bom = bytes.Length >= 3 && bytes.[0] = 0xEFuy && bytes.[1] = 0xBBuy && bytes.[2] = 0xBFuy
        let text = System.IO.File.ReadAllText projectFile
        let m = rangeRx.Match text
        if not m.Success then failwith $"{projectFile}: no default XakeVersion range to move"
        let encoding = System.Text.UTF8Encoding (encoderShouldEmitUTF8Identifier = bom)
        System.IO.File.WriteAllText (projectFile, rangeRx.Replace (text, "${1}" + floor + "${3}", 1), encoding)
        m.Groups.[2].Value

// Own database (.xake.fsc): the phony targets (build, test, clean) share names with build.fsx's.
do xake { ExecOptions.Default with DbFileName = ".xake.fsc" } {
    filelog "build.log" Verbosity.Chatty
    varschema vars
    var "NUGET_PACKAGES" ".packages"   // a default: -d NUGET_PACKAGES=<dir> (from Xake 4.0 also $XAKE__NUGET_PACKAGES) overrides it

    // as build.fsx: the jobs of testjobs.fsx in parallel, the test projects built once by
    // msbuild (`dotnet build`); no --blame-hang here
    rules (testRules (recipe { return [] }) [] vars.TestFilter)
    rules [
        "main" <<< ["build"; "test"]
        "clean" => rm { dir "out/Release" }   // out/netstandard2.0 is build.fsx's

        // (a) project file -> description, under msbuild; records the msbuild files it read
        command "import" {
            let! imported = Import.descriptions (Fsproj.For projects) Description.beside []
            for i in imported do do! Fingerprints.record fingerprints i.Description i.Inputs
        }
        // (b) description -> lock, referenced projects first; `-d LOCK_REFRESH=FSharp.Core` (or `*`) re-resolves.
        // Lock rules run one at a time (`for`): each records its line into the one fingerprint file.
        Lock.Rule <| fun m -> recipe {
            let! d = Description.load (descriptionOf m.name)
            for r in d.ProjectRefs do do! need [ Lock.Format { m with name = r } ]
            do! Locks.generate lockOf outOf m.cfg (descriptionOf m.name)
            do! Fingerprints.record fingerprints (LockFile.Format m) (descriptionOf m.name :: LockFile.For [ for r in d.ProjectRefs -> { m with name = r } ])
        }
        command "update-locks" { for p in projects do do! need [ Lock.Format { name = p.name; cfg = "Release" } ] }

        // Moves the Xake floor (release-process E1): -d XAKE_FLOOR=<v> (default: the newest Xake on
        // nuget.org; previews count while no 4.x release exists) becomes the floor of
        // `XakeVersion` in src/dotnet's project file, the descriptions are re-imported, and Xake
        // is re-resolved in the locks only. Refuses a version nuget.org does not list (a floor
        // that does not exist warns NU1603 on every restore). Commit the project file,
        // descriptions, locks and xake.fingerprints.
        command "bump-base" {
            let! requested = vars.XakeFloor
            let published = XakeFloor.published ()
            let floor = match requested with Some v -> v | None -> XakeFloor.newest published
            if not (List.contains floor published) then
                failwith $"Xake {floor} is not on nuget.org; a floor that does not exist warns NU1603 on every restore"
            let previous = XakeFloor.write floor
            do! trace Level.Message "Xake floor %s -> %s (%s)" previous floor XakeFloor.projectFile
            // right after a release the local NuGet HTTP cache still lists the old versions (it
            // keeps a package's version list for 30 minutes) and the lock's restore fails NU1102
            if previous <> floor then
                do! sh "dotnet nuget locals http-cache --clear" { failonerror }
            do! need [ "import" ]
            // the lock keeps the Xake it holds unless LOCK_REFRESH names it; the lock rule reads
            // the script variable, else the environment, and this run asks for nothing else
            System.Environment.SetEnvironmentVariable ("LOCK_REFRESH", "Xake")
            do! need [ "update-locks" ]
        }
        "check" => Fingerprints.check fingerprints

        // (c) every described project's Release outputs, from the committed files only (fresh clone, CI)
        command "build" {
            do! Fingerprints.warn fingerprints
            do! Locks.needAll (Desc.Glob Desc.Any) "Release" lockOf outOf
        }
        Dll.Rule <| fun m -> Locks.compileAs (lockOf m.name m.cfg) m.fwk versions
        // pack (nupkg from out/Release, no second msbuild run) comes with Package.nupkg (fsc-rework PR 8)
    ]
}
