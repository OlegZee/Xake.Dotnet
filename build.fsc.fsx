// Declarative self-build (docs/hermetic/lock.md): what a project is lives in src/<dir>/xake.project.json,
// how it compiles in locks/<name>/Release.json; both committed, written only by `import` and `update-locks`.
// `build` compiles from them with fsc, no msbuild; `check` fails where `build` only warns (xake.fingerprints).
// Bootstrap: the published packages (the exact published version, bumped only after a release lands on nuget.org).
#r "nuget: Xake, 3.15.0"
#r "nuget: Xake.Hermetic.Dotnet, 0.10.0"
#load "testjobs.fsx"   // the test jobs, shared with build.fsx

open Xake
open Xake.Tasks
open Xake.Dotnet
open Xake.Hermetic.Dotnet
open Testjobs

let vars = {|
    Version    = Var.create<string>(description = "Xake.Dotnet package version (both assemblies)") |> withDefault "0.0.1"
    TestFilter = Var.string(cliArg = "FILTER", envVar = "FILTER", description = "Runs only the tests whose name matches this regex, e.g. 'Lock'")
    XakeFloor  = Var.string(cliArg = "XAKE_FLOOR", description = "bump-base: the Xake version to build against (default: the newest on nuget.org)")
|}

// The engine is the published `Xake` package (a package reference in the descriptions), not a
// project of this script.
let projects = [ "Xake.Dotnet", "src/dotnet"; "Xake.Hermetic.Dotnet", "src/hermetic" ]
let descriptions = !! "src/*/xake.project.json"
let dirOf = dict projects
let descriptionOf name = $"{dirOf[name]}/xake.project.json"
let lockOf name cfg = $"locks/{name}/{cfg}.json"
let outOf cfg fwk (_: string) = $"out/{cfg}/{fwk}"
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
    /// it replaced.
    let write (floor: string) =
        let text = System.IO.File.ReadAllText projectFile
        let m = rangeRx.Match text
        if not m.Success then failwith $"{projectFile}: no default XakeVersion range to move"
        System.IO.File.WriteAllText (projectFile, rangeRx.Replace (text, "${1}" + floor + "${3}", 1))
        m.Groups.[2].Value

// Own database (.xake.fsc): the phony targets (build, test, clean) share names with build.fsx's.
do xake { ExecOptions.Default with DbFileName = ".xake.fsc" } {
    filelog "build.log" Verbosity.Chatty
    varschema vars
    var "NUGET_PACKAGES" ".packages"   // a default: -d NUGET_PACKAGES=<dir> (from Xake 4.0 also $XAKE__NUGET_PACKAGES) overrides it

    rules [
        "main" <<< ["build"; "test"]
        "clean" => rm { dir "out/Release" }   // out/netstandard2.0 is build.fsx's

        // (a) project file -> description, under msbuild; records the msbuild files it read
        command "import" {
            let! imported = Import.descriptions [ for name, dir in projects -> $"{dir}/{name}.fsproj" ] Description.beside []
            for i in imported do do! Fingerprints.record fingerprints i.Description i.Inputs
        }
        // (b) description -> lock, referenced projects first; `-d LOCK_REFRESH=FSharp.Core` (or `*`) re-resolves.
        // Lock rules run one at a time (`for`): each records its line into the one fingerprint file.
        "lock::(name:*)::(cfg:*)" => recipe {
            let! name = getRuleMatch "name"
            let! cfg  = getRuleMatch "cfg"
            let! d = Description.load (descriptionOf name)
            for r in d.ProjectRefs do do! need [ $"lock::{r}::{cfg}" ]
            do! alwaysRerun ()
            do! Locks.generate lockOf outOf cfg (descriptionOf name)
            do! Fingerprints.record fingerprints (lockOf name cfg) (descriptionOf name :: [ for r in d.ProjectRefs -> lockOf r cfg ])
        }
        command "update-locks" { for name, _ in projects do do! need [ $"lock::{name}::Release" ] }

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
            do! Locks.needAll descriptions "Release" lockOf outOf
        }
        target "out/(cfg:*)/(fwk:*)/(name:*).dll" {
            let! cfg = getRuleMatch "cfg"
            let! fwk = getRuleMatch "fwk"
            let! name = getRuleMatch "name"
            do! Locks.compileAs (lockOf name cfg) fwk versions
        }

        // as build.fsx: the jobs of testjobs.fsx in parallel, the test projects built once by
        // msbuild (`dotnet build`); no --blame-hang here
        "test" <== [ for j in testJobs -> $"test::{testJobName j}" ]
        "test-build::(project:*)" => recipe {
            let! project = getRuleMatch "project"
            do! sh $"dotnet build {testProjectDir project} -c Release" {
                logprefix $"[test-build {project}]"
                stdoutlevel (fun l -> if l.Contains ": error " then Level.Error else Level.Info)
                failonerror }
        }
        "test::(project:*)/(slice:*)" => recipe {
            let! project = getRuleMatch "project"
            let! slice = getRuleMatch "slice"
            let job = testJob project slice
            do! need [ $"test-build::{project}" ]
            do! need (testJobNeeds job)   // the compiler server's users, one after another
            let! filter = vars.TestFilter
            resetTestJobDir job
            do! sh $"dotnet test {testProjectDir project} -c Release --no-build" {
                args (testArgs job filter)
                env (testJobEnv job)
                logprefix $"[{testJobName job}]"
                stdoutlevel (fun l -> if isTestSummary l then Level.Message else Level.Info)
                captureOutput (ToFile (Both, testJobDir job + "/console.log"))
                failonerror }
        }
        // pack (nupkg from out/Release, no second msbuild run) comes with Package.nupkg (fsc-rework PR 8)
    ]
}
