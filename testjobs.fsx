// The test jobs of `test`, loaded by build.fsx and build.fsc.fsx so both run the same slices.
// The jobs are plain data; `testRules` turns them into the rules (`test`, `test-build::<project>`,
// `test::<project>/<slice>`), which differ in the scripts by their arguments only.
//
// A job is a test project and a slice of its fixtures (`dotnet/jobs`, `hermetic/run`, ...); the
// slices of a project are made of whole fixture classes, since each fixture owns its working
// folder (src/dotnet.tests/~testout~/<folder>, XakeTestBase), so two jobs never share one. The last
// slice of a project (`Rest`) is every fixture not named by the project's other slices: each
// test is in exactly one job. Slices are cut to roughly even durations (seconds measured on
// 18 logical CPUs, 2026-10); a new fixture lands in `rest` until it is worth a slice of its own.
//
// Fixtures that use the compiler server are serial: the server (VBCSCompiler) is one per
// machine, and a neighbour starting it, loading it or letting its keepalive expire makes a
// client fall back to a plain csc, which fails a test that asserts the server. They sit in the
// `Server = true` jobs, run one after another in list order; every other job runs with
// XAKE_CSC_SERVER=off (csc in-process), so it never touches the server; XakeTestBase passes the
// environment variable to the test scripts as -d (Xake.Dotnet reads build variables only).

open Xake
open Xake.Tasks
open Xake.Typed

type Slice =
    /// these fixture classes (full names, namespace included)
    | Fixtures of string list
    /// every fixture of the project not named by its other slices
    | Rest

/// A job's key, `test::<project>/<slice>`: the test project (`dotnet` is src/dotnet.tests) and the slice's name.
type JobG = { project: string; slice: string }
type TestJob = { Key: JobG; Slice: Slice; Server: bool }

let private job project name slice = { Key = { project = project; slice = name }; Slice = slice; Server = false }

let testJobs = [
    // the compiler server's users, serial: dotnet/server, then hermetic/server
    { job "dotnet" "server" (Fixtures [ "Tests.Csc compiler server" ]) with Server = true }                 // ~2 s
    { job "hermetic" "server" (Fixtures [ "Tests.Building from Xake-generated locks"                       // ~32 s
                                          "Tests.Compile under the compiler server" ]) with Server = true }
    job "dotnet" "jobs" (Fixtures [ "Tests.Compiler jobs" ])
    job "dotnet" "rest" Rest
    job "hermetic" "record" (Fixtures [ "Tests.Import record"; "Tests.Import record project chain"           // ~34 s
                                        "Tests.Import record configurations"; "Tests.Import record refusals and descriptions"
                                        "Tests.Import record switch files"; "Tests.Import record concurrent brands" ])
    job "hermetic" "run" (Fixtures [ "Tests.Run folders"; "Tests.Import record applications"; "Tests.Description import" ])  // ~36 s
    job "hermetic" "lock" (Fixtures [ "Tests.Lock generation"; "Tests.Described compilation"                 // ~32 s
                                      "Tests.Fsc lock"; "Tests.Csc fromlock"; "Tests.Csc lock" ])
    job "hermetic" "rest" Rest                                                                                // ~18 s
]

let TestJob = Command<JobG> "test::(project:*)/(slice:*)"
let TestBuild = Command<string> "test-build::(project:*)"
let TestSrc = Target<string> "src/(project:*).tests"
/// Where a job writes its trx, console log and blame files: a folder of its own.
let TestOut = Target<JobG> "out/test/(project:*)/(slice:*)"
let TestLog = Target<JobG> "out/test/(project:*)/(slice:*)/console.log"

/// The jobs a job waits for: a server job, for the server job before it.
let testJobNeeds job =
    if not job.Server then [] else
    testJobs |> List.filter (fun j -> j.Server) |> List.takeWhile ((<>) job) |> List.rev |> List.truncate 1
    |> List.map (fun j -> TestJob.Format j.Key)

/// The compiler server switch of a job's `dotnet test`: on for the server jobs (their tests
/// need it), off for every other job (it never touches the server).
let testJobEnv job = "XAKE_CSC_SERVER", (if job.Server then "on" else "off")

/// Empties a job's folder before its run: only the last run's files are kept.
let resetTestJobDir job =
    let dir = TestOut.Format job.Key
    if System.IO.Directory.Exists dir then System.IO.Directory.Delete (dir, true)
    System.IO.Directory.CreateDirectory dir |> ignore

/// The NUnit selection of a job (`NUnit.Where`, exact class names), `&& name =~ '<filter>'`
/// added when FILTER is set. Not vstest's `--filter`: the NUnit adapter (4.2) converts it to
/// this syntax and fails on a class name with a space in it.
let testWhereOf job (filter: string option) =
    let mine = function Fixtures fs -> fs | Rest -> []
    let anyOf fs = [ for f in fs -> $"class == '{f}'" ] |> String.concat " || "
    let slice =
        match job.Slice with
        | Fixtures fs -> anyOf fs
        | Rest ->
            match [ for j in testJobs do if j.Key.project = job.Key.project then yield! mine j.Slice ] with
            | [] -> ""
            | others -> $"!({anyOf others})"
    match slice, filter with
    | "", None -> None
    | "", Some f -> Some $"name =~ '{f}'"
    | s, None -> Some s
    | s, Some f -> Some $"({s}) && name =~ '{f}'"

/// `dotnet test` arguments of a job (after the project and `-c Release --no-build`); they end
/// with `-- NUnit.Where=...`, so other options go before them.
let testArgs job filter =
    [ yield! [ "--results-directory"; TestOut.Format job.Key; "--logger"; "\"trx;LogFileName=test.trx\"" ]
      for w in Option.toList (testWhereOf job filter) do yield! [ "--"; $"\"NUnit.Where={w}\"" ] ]

/// The lines of `dotnet test` output that go to the console: the summary and the failed tests
/// (the tests' own output, expected compiler errors among it, stays in console.log).
let isTestSummary (line: string) =
    let l = line.TrimStart()
    l.StartsWith "Passed!" || l.StartsWith "Failed!" || l.StartsWith "Skipped!" || l.StartsWith "Failed "

/// The test rules. The test projects are built once by msbuild (`buildArgs` are added to `dotnet
/// build`), each job runs `dotnet test --no-build` with `testExtra` before its own arguments.
let testRules (buildArgs: Recipe<ExecContext, string list>) (testExtra: string list) (filter: OptionalVar<string>) = [
    "test" <== TestJob.For [ for j in testJobs -> j.Key ]
    TestBuild.Rule <| fun project -> recipe {
        let! extra = buildArgs
        do! sh $"dotnet build {TestSrc.Format project} -c Release" {
            args extra
            logprefix $"[test-build {project}]"
            stdoutlevel (fun l -> if l.Contains ": error " then Level.Error else Level.Info)
            failonerror }
    }
    TestJob.Rule <| fun m -> recipe {
        let job = testJobs |> List.find (fun j -> j.Key = m)
        do! need [ TestBuild.Format m.project ]
        do! need (testJobNeeds job)   // the compiler server's users, one after another
        let! filter = filter
        resetTestJobDir job
        do! sh $"dotnet test {TestSrc.Format m.project} -c Release --no-build" {
            args (testExtra @ testArgs job filter)
            env (testJobEnv job)
            logprefix $"[{m.project}/{m.slice}]"
            stdoutlevel (fun l -> if isTestSummary l then Level.Message else Level.Info)
            captureOutput (ToFile (Both, TestLog.Format m))
            failonerror }
    }
]
