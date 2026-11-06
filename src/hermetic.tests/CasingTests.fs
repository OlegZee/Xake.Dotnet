namespace Tests

open System
open System.IO
open System.Diagnostics
open NUnit.Framework

open Xake
open Xake.Dotnet
open Xake.Hermetic.Dotnet

/// What the casing tests share: a temporary git repository (outside this one) and git itself.
module internal CasingKit =

    /// `git <args>` in `dir`, failing with its output on a non-zero exit; its standard output.
    let git (dir: string) (args: string list) =
        let psi = ProcessStartInfo ("git")
        psi.WorkingDirectory <- dir
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        for a in args do psi.ArgumentList.Add a
        use p = Process.Start psi
        let out = p.StandardOutput.ReadToEnd ()
        let err = p.StandardError.ReadToEnd ()
        p.WaitForExit ()
        if p.ExitCode <> 0 then failwithf "'git %s' failed (exit %d):\n%s\n%s" (String.Join (" ", args)) p.ExitCode out err
        out

    /// A new empty folder under the system's temp folder, `/`-separated.
    let tempFolder (label: string) =
        let dir = Path.Combine (Path.GetTempPath (), sprintf "xake-%s-%s" label (Guid.NewGuid().ToString "N"))
        Directory.CreateDirectory dir |> ignore
        dir.Replace ('\\', '/')

    /// A git repository at a new temp folder with `files` (path, text) written and added.
    let repository (label: string) (files: (string * string) list) =
        let dir = tempFolder label
        git dir [ "init"; "-q" ] |> ignore
        for path, text in files do
            let full = Path.Combine (dir, path)
            Directory.CreateDirectory (Path.GetDirectoryName full) |> ignore
            File.WriteAllText (full, text)
        if not (List.isEmpty files) then git dir ([ "add"; "--" ] @ List.map fst files) |> ignore
        dir

    /// Whether the file system of the temp folder ignores case (macOS, Windows).
    let tempIgnoresCase () =
        let dir = tempFolder "case-probe"
        try
            File.WriteAllText (Path.Combine (dir, "probe.txt"), "")
            File.Exists (Path.Combine (dir, "PROBE.TXT"))
        finally Directory.Delete (dir, true)

    let ok = function Result.Ok i -> i | Result.Error (e: string) -> failwith e


/// `Casing`: the repository's spelling of a path from the git index.
[<TestFixture>]
type ``Casing``() =

    [<Test>]
    member x.``a path in the index takes its spelling, folders included; outside the root or untracked it stays``() =
        let root = CasingKit.repository "casing" [ "foo/Bar.cs", "class Bar { }\n"; "docs/sub/Common.xml", "<doc/>\n"; "Top.txt", "" ]
        try
            let index = Casing.read root |> CasingKit.ok
            let resolve = Casing.resolve index
            Assert.That(resolve (root + "/Foo/BAR.cs"), Is.EqualTo (root + "/foo/Bar.cs"))
            Assert.That(resolve (root + "/DOCS/Sub/common.XML"), Is.EqualTo (root + "/docs/sub/Common.xml"))
            Assert.That(resolve (root + "/top.TXT"), Is.EqualTo (root + "/Top.txt"))
            // a folder, with and without its slash; `\` read as `/`
            Assert.That(resolve (root + "/Docs/SUB"), Is.EqualTo (root + "/docs/sub"))
            Assert.That(resolve (root + "/Docs/SUB/"), Is.EqualTo (root + "/docs/sub/"))
            Assert.That(resolve (root.Replace ('/', '\\') + "\\FOO\\bar.cs"), Is.EqualTo (root + "/foo/Bar.cs"))
            Assert.That(resolve (root + "/sub/../Foo/BAR.cs"), Is.EqualTo (root + "/foo/Bar.cs"))
            // the repository's spelling already: the very same string back
            let exact = root + "/foo/Bar.cs"
            Assert.That(Object.ReferenceEquals (resolve exact, exact), Is.True)
            // outside the root, not in the index: unchanged
            Assert.That(resolve "/elsewhere/Foo/BAR.cs", Is.EqualTo "/elsewhere/Foo/BAR.cs")
            Assert.That(resolve (root + "/Foo/Untracked.cs"), Is.EqualTo (root + "/Foo/Untracked.cs"))
            Assert.That(resolve (root + "x/foo/Bar.cs"), Is.EqualTo (root + "x/foo/Bar.cs"))
        finally Directory.Delete (root, true)

    [<Test>]
    member x.``an index read for a subfolder lists the files under it, relative to it``() =
        let top = CasingKit.repository "casing-sub" [ "Repo/Src/A.cs", ""; "Other/B.cs", "" ]
        try
            let index = Casing.read (top + "/Repo") |> CasingKit.ok
            Assert.That(Casing.resolve index (top + "/Repo/src/a.CS"), Is.EqualTo (top + "/Repo/Src/A.cs"))
            Assert.That(Casing.resolve index (top + "/other/b.cs"), Is.EqualTo (top + "/other/b.cs"))
        finally Directory.Delete (top, true)

    [<Test>]
    member x.``two entries that differ only by case: the exact one wins, else the import fails naming both``() =
        let root = CasingKit.repository "casing-dup" []
        try
            // index entries without files: the same on a file system that ignores case
            File.WriteAllText (Path.Combine (root, "blob.txt"), "x\n")
            let blob = (CasingKit.git root [ "hash-object"; "-w"; "blob.txt" ]).Trim ()
            for path in [ "Dup/x.cs"; "dup/X.cs" ] do
                CasingKit.git root [ "update-index"; "--add"; "--cacheinfo"; "100644," + blob + "," + path ] |> ignore
            let index = Casing.read root |> CasingKit.ok
            Assert.That(Casing.resolve index (root + "/Dup/x.cs"), Is.EqualTo (root + "/Dup/x.cs"))
            Assert.That(Casing.resolve index (root + "/dup/X.cs"), Is.EqualTo (root + "/dup/X.cs"))
            let e = Assert.Catch(fun () -> Casing.resolve index (root + "/DUP/X.CS") |> ignore)
            Assert.That(e.Message, Does.Contain "'Dup/x.cs', 'dup/X.cs'")
            Assert.That(e.Message, Does.Contain "differ only by case")
            // a folder of two spellings, exact or failing the same way
            Assert.That(Casing.resolve index (root + "/dup"), Is.EqualTo (root + "/dup"))
            Assert.Catch(fun () -> Casing.resolve index (root + "/DUP") |> ignore) |> ignore
        finally Directory.Delete (root, true)

    [<Test>]
    member x.``outside a repository there is no index, and a resolver without one changes nothing``() =
        let dir = CasingKit.tempFolder "casing-none"
        try
            match Casing.read dir with
            | Result.Ok _ -> Assert.Fail "an index outside a repository"
            | Result.Error e -> Assert.That(e, Does.Contain "git")
            Assert.That(Casing.Resolver.Unchanged.Path (dir + "/Foo.cs"), Is.EqualTo (dir + "/Foo.cs"))
            Assert.That(Casing.Resolver.Unchanged.Differences, Is.Empty)
        finally Directory.Delete (dir, true)

    [<Test>]
    member x.``a resolver keeps a relative path relative, and notes each difference once``() =
        let root = CasingKit.repository "casing-rel" [ "lib/Strings.resx", ""; "app/Key.snk", "" ]
        try
            let r = Casing.Resolver (Some (Casing.Repos []))
            let app = root + "/app"
            Assert.That(r.Relative app "../LIB/strings.RESX", Is.EqualTo "../lib/Strings.resx")
            Assert.That(r.Relative app "key.SNK", Is.EqualTo "Key.snk")
            Assert.That(r.Relative app "Key.snk", Is.EqualTo "Key.snk")
            Assert.That(r.Relative app "Missing.snk", Is.EqualTo "Missing.snk")
            Assert.That(r.Relative app (root + "/App/KEY.snk"), Is.EqualTo (root + "/app/Key.snk"))
            r.Path (root + "/app/key.SNK") |> ignore
            Assert.That(r.Differences,
                        Is.EqualTo [ root + "/App/KEY.snk", root + "/app/Key.snk"
                                     root + "/LIB/strings.RESX", root + "/lib/Strings.resx"
                                     root + "/app/key.SNK", root + "/app/Key.snk" ])
        finally Directory.Delete (root, true)

    [<Test>]
    member x.``a command line takes the repository's paths; an unnamed resource keeps its file name as its name``() =
        let root = CasingKit.repository "casing-args" [ "app/code/Program.cs", ""; "app/res/logo.png", ""; "app/keys/app.snk", "" ]
        try
            let r = Casing.Resolver (Some (Casing.Repos []))
            let csc = Casing.argument false r
            Assert.That(csc (root + "/app/Code/program.CS"), Is.EqualTo (root + "/app/code/Program.cs"))
            Assert.That(csc ("/keyfile:" + root + "/app/Keys/App.SNK"), Is.EqualTo ("/keyfile:" + root + "/app/keys/app.snk"))
            // a name given: kept as it is; none: the project's file name becomes the name
            Assert.That(csc ("/resource:" + root + "/app/Res/Logo.PNG,App.Res.Logo.PNG"), Is.EqualTo ("/resource:" + root + "/app/res/logo.png,App.Res.Logo.PNG"))
            Assert.That(csc ("/resource:" + root + "/app/Res/Logo.PNG"), Is.EqualTo ("/resource:" + root + "/app/res/logo.png,Logo.PNG"))
            // only the folder differs: the file name, hence the name, is the same
            Assert.That(csc ("/resource:" + root + "/app/RES/logo.png"), Is.EqualTo ("/resource:" + root + "/app/res/logo.png"))
            Assert.That(csc "/optimize+", Is.EqualTo "/optimize+")
            Assert.That(csc ("/resource:" + root + "/obj/X.resources"), Is.EqualTo ("/resource:" + root + "/obj/X.resources"))
            let fsc = Casing.argument true r
            Assert.That(fsc ("--resource:" + root + "/app/Res/Logo.PNG"), Is.EqualTo ("--resource:" + root + "/app/res/logo.png,Logo.PNG"))
            Assert.That(fsc (root + "/app/CODE/Program.cs"), Is.EqualTo (root + "/app/code/Program.cs"))
        finally Directory.Delete (root, true)

    [<Test>]
    member x.``a mask's literal part takes the repository's spelling, its wildcards stay``() =
        let root = CasingKit.repository "casing-masks" [ "app/src/Sub/A.cs", ""; "app/Gen (x86)/B.cs", "" ]
        try
            let r = Casing.Resolver (Some (Casing.Repos []))
            let fix = Import.repositoryMask r (root + "/app")
            Assert.That(fix "SRC/sub/a.CS", Is.EqualTo "src/Sub/A.cs")
            Assert.That(fix "Src/**/*.cs", Is.EqualTo "src/**/*.cs")
            Assert.That(fix "!SRC/SUB/*.cs", Is.EqualTo "!src/Sub/*.cs")
            Assert.That(fix "**/*.cs", Is.EqualTo "**/*.cs")
            Assert.That(fix "src/**/*.cs", Is.EqualTo "src/**/*.cs")
            Assert.That(fix "Missing/*.cs", Is.EqualTo "Missing/*.cs")
            // escapes (D45) read and written back
            Assert.That(fix @"gen \(X86\)/*.cs", Is.EqualTo @"Gen \(x86\)/*.cs")
        finally Directory.Delete (root, true)

    [<Test>]
    member x.``a ResXFileRef and a doc comment include spelled otherwise are reported, with both spellings``() =
        let resx =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n"
            + "  <data name=\"Copy\" type=\"System.Resources.ResXFileRef, System.Windows.Forms\"><value>..\\Images\\Copy.TXT;System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></data>\n"
            + "  <data name=\"Same\" type=\"System.Resources.ResXFileRef, System.Windows.Forms\"><value>..\\images\\same.txt;System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></data>\n"
            + "</root>\n"
        let source =
            "namespace App {\n"
            + "  /// <include file='Docs\\Common.XML' path='doc/member[@name=\"C\"]/*'/>\n"
            + "  /// <include file=\"docs/common.xml\" path='doc/member[@name=\"D\"]/*'/>\n"
            + "  // <include file='Docs\\Other.XML' path='x'/> is no doc comment\n"
            + "  public class C { }\n}\n"
        let root =
            CasingKit.repository "casing-content"
                [ "app/resources/Strings.resx", resx; "app/images/copy.txt", "copy"; "app/images/same.txt", "same"
                  "app/code/C.cs", source; "app/code/docs/common.xml", "<doc/>"; "app/code/docs/other.xml", "<doc/>" ]
        try
            let r = Casing.Resolver (Some (Casing.Repos []))
            let lines = Import.contentLines root r (Some (fun _ -> true)) (root + "/app") [ "resources/Strings.resx" ] [ root + "/app/code/C.cs" ]
            Assert.That(lines, Has.Length.EqualTo 2)
            Assert.That(lines.[0], Does.StartWith "'app/Images/Copy.TXT' (ResXFileRef '..\\Images\\Copy.TXT' in 'app/resources/Strings.resx') is 'app/images/copy.txt' in the repository")
            Assert.That(lines.[1], Does.StartWith "'app/code/Docs/Common.XML' (<include file='Docs\\Common.XML'> in 'app/code/C.cs') is 'app/code/docs/common.xml' in the repository")
            Assert.That(lines.[1], Does.Contain "fix the spelling in the source")
            // reported, not noted as a recorded difference
            Assert.That(r.Differences, Is.Empty)
        finally Directory.Delete (root, true)


    [<Test>]
    member x.``a recorded deps.json names a referenced project's file as the build writes it, its library and assembly names kept``() =
        let deps =
            String.concat "\n"
                [ "{"
                  "  \"targets\": {"
                  "    \".NETCoreApp,Version=v8.0\": {"
                  "      \"Tool/1.0.0\": {"
                  "        \"dependencies\": {"
                  "          \"core\": \"$(AssemblyVersion:core.dll)\""
                  "        },"
                  "        \"runtime\": {"
                  "          \"Tool.dll\": {}"
                  "        }"
                  "      },"
                  "      \"core/$(AssemblyVersion:core.dll)\": {"
                  "        \"runtime\": {"
                  "          \"core.dll\": {"
                  "            \"assemblyVersion\": \"$(AssemblyVersion:core.dll)\","
                  "            \"fileVersion\": \"$(FileVersion:core.dll)\""
                  "          }"
                  "        }"
                  "      },"
                  "      \"Other/1.0.0\": { \"runtime\": { \"lib/net8.0/core.dll\": {} } }"
                  "    }"
                  "  },"
                  "  \"libraries\": {"
                  "    \"core/$(AssemblyVersion:core.dll)\": { \"type\": \"project\", \"serviceable\": false, \"sha512\": \"\" }"
                  "  }"
                  "}" ]
        let renamed = Import.depsFileNames [ "core.dll", "Core.dll" ] deps
        Assert.That(renamed, Does.Contain "\"Core.dll\": {")
        Assert.That(renamed, Does.Not.Contain "\"core.dll\"")
        Assert.That(renamed, Does.Not.Contain ":core.dll)")
        // the library keeps msbuild's name, a package's file in a folder is another file
        Assert.That(renamed, Does.Contain "\"core/$(AssemblyVersion:Core.dll)\": {")
        Assert.That(renamed, Does.Contain "\"core\": \"$(AssemblyVersion:Core.dll)\"")
        Assert.That(renamed, Does.Contain "\"lib/net8.0/core.dll\"")
        Assert.That(Import.depsFileNames [] deps, Is.EqualTo deps)

    [<Test>]
    member x.``each path takes the spelling of the repository that contains it: a nested one, a submodule``() =
        let outer = CasingKit.repository "casing-outer" [ "Top.txt", "" ]
        let lib = CasingKit.repository "casing-sublib" [ "Code/Lib.cs", "" ]
        try
            CasingKit.git lib [ "-c"; "user.name=t"; "-c"; "user.email=t@t"; "commit"; "-q"; "-m"; "lib" ] |> ignore
            // a repository of its own inside the outer one (as a test's folder in this one)
            let inner = outer + "/work"
            Directory.CreateDirectory (inner + "/app/src") |> ignore
            File.WriteAllText (inner + "/app/src/A.cs", "")
            CasingKit.git inner [ "init"; "-q" ] |> ignore
            CasingKit.git inner [ "add"; "--"; "app/src/A.cs" ] |> ignore
            // a submodule of the inner one, at `ext/Lib`
            CasingKit.git inner [ "-c"; "protocol.file.allow=always"; "submodule"; "add"; "-q"; lib; "ext/Lib" ] |> ignore
            let repos = Casing.Repos []
            Assert.That(repos.Resolve (inner + "/APP/Src/a.CS"), Is.EqualTo (inner + "/app/src/A.cs"))
            Assert.That(repos.Resolve (outer + "/top.TXT"), Is.EqualTo (outer + "/Top.txt"))
            // in the submodule: its own index, its path as its superproject has it
            Assert.That(repos.Resolve (inner + "/ext/Lib/code/lib.CS"), Is.EqualTo (inner + "/ext/Lib/Code/Lib.cs"))
            Assert.That(repos.Resolve (inner + "/EXT/lib/CODE/Lib.cs"), Is.EqualTo (inner + "/ext/Lib/Code/Lib.cs"))
            // an untracked file of a repository, a folder in none: unchanged
            File.WriteAllText (inner + "/app/src/Untracked.cs", "")
            Assert.That(repos.Resolve (inner + "/APP/src/untracked.cs"), Is.EqualTo (inner + "/APP/src/untracked.cs"))
            let none = CasingKit.tempFolder "casing-none2"
            try
                Assert.That(repos.Resolve (none + "/X.CS"), Is.EqualTo (none + "/X.CS"))
                Assert.That(repos.Outside |> List.map fst, Is.EqualTo [ none ])
            finally Directory.Delete (none, true)
            // excluded folders are never looked up
            let excluding = Casing.Repos [ inner + "/app" ]
            Assert.That(excluding.Resolve (inner + "/app/src/a.CS"), Is.EqualTo (inner + "/app/src/a.CS"))
        finally
            Directory.Delete (outer, true)
            Directory.Delete (lib, true)

/// The files a `.resx` names, as an import records them (`Csc.ResxFiles`): the lock's
/// `ResxFiles` and the resx compile that reads them.
[<TestFixture>]
type ``Resx file references``() =

    let roots = [ "$(NuGetPackageRoot)", "/r/pkgs"; "$(ProjectRoot)", "/r/proj"; "$(DotnetRoot)", "/r/dotnet" ]

    let entry (files: ResxFile list) : Lock.Entry =
        let c =
            { Csc.ofArgs [ "/noconfig"; "/target:library"; "/resource:/r/proj/obj/App.Strings.resources"; "/r/proj/app/A.cs" ] with
                Name = "App"; Framework = "net8.0"; Directory = "/r/proj/app"
                Resources = [ "/r/proj/app/resources/Strings.resx", "/r/proj/obj/App.Strings.resources" ]
                ResxFiles = files }
        { Lock.ofCsc c with Evaluation = { Lock.Evaluation.Empty with Project = "/r/proj/app/App.csproj"; Sdk = "8.0.425" } }

    [<Test>]
    member x.``the lock writes ResxFiles only when there are, and reads a lock without the key``() =
        let file : ResxFile =
            { Resx = "/r/proj/app/resources/Strings.resx"; Ref = "..\\Images\\Copy.TXT"; Path = "/r/proj/app/images/copy.txt"; Sha256 = "ab" }
        let text = Lock.format roots { Configuration = "Release"; Properties = []; Entries = [ entry [ file ] ] }
        Assert.That(text, Does.Contain "\"ResxFiles\": [")
        Assert.That(text, Does.Contain "{ \"Resx\": \"$(ProjectRoot)/app/resources/Strings.resx\", \"Ref\": \"..\\\\Images\\\\Copy.TXT\", \"Path\": \"$(ProjectRoot)/app/images/copy.txt\", \"Sha256\": \"ab\" }")
        Assert.That(text, Does.StartWith "{\n  \"Format\": 1,\n")
        let back = Lock.parse roots text
        Assert.That(back.Entries.Head.Compilation.ResxFiles, Is.EqualTo [ file ])
        Assert.That(Lock.format roots back, Is.EqualTo text)
        // without: no key (the text of every other lock as before), and such a text reads as none
        let plain = Lock.format roots { Configuration = "Release"; Properties = []; Entries = [ entry [] ] }
        Assert.That(plain, Does.Not.Contain "ResxFiles")
        Assert.That((Lock.parse roots plain).Entries.Head.Compilation.ResxFiles, Is.Empty)
        // a lock diff names the file
        Assert.That(Lock.diff (entry []) (entry [ file ]), Is.EqualTo [ "+ ResxFile /r/proj/app/resources/Strings.resx ..\\Images\\Copy.TXT" ])
        Assert.That(Lock.diff (entry [ file ]) (entry [ { file with Path = "/r/proj/app/Images/Copy.TXT" } ]),
                    Is.EqualTo [ "~ ResxFile /r/proj/app/resources/Strings.resx ..\\Images\\Copy.TXT: /r/proj/app/images/copy.txt -> /r/proj/app/Images/Copy.TXT" ])

    [<Test>]
    member x.``the resx compile reads a ResXFileRef from the file recorded for its spelling``() =
        let dir = CasingKit.tempFolder "resx-files"
        try
            Directory.CreateDirectory (dir + "/resources") |> ignore
            Directory.CreateDirectory (dir + "/stored") |> ignore
            let resx = dir + "/resources/Strings.resx"
            File.WriteAllText (resx,
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n"
                + "  <data name=\"Copy\" type=\"System.Resources.ResXFileRef, System.Windows.Forms\"><value>..\\Nowhere\\Copy.TXT;System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></data>\n"
                + "</root>\n")
            File.WriteAllText (dir + "/stored/copy.txt", "the recorded file")
            // as spelled the file is not there: the resx alone does not compile
            Assert.Catch(fun () -> ResxFormat.read resx |> ignore) |> ignore
            let entries = ResxFormat.readWith [ "..\\Nowhere\\Copy.TXT", dir + "/stored/copy.txt" ] resx
            Assert.That(entries, Is.EqualTo [ ResxFormat.Text ("Copy", "the recorded file") ])
            let output = dir + "/out/App.Strings.resources"
            Assert.That(Resx.refreshWith [ "..\\Nowhere\\Copy.TXT", dir + "/stored/copy.txt" ] resx output, Is.True)
            use reader = new System.Resources.Extensions.DeserializingResourceReader (output)
            let values = [ for e in reader |> Seq.cast<System.Collections.DictionaryEntry> -> string e.Key, string e.Value ]
            Assert.That(values, Is.EqualTo [ "Copy", "the recorded file" ])
        finally Directory.Delete (dir, true)


/// `Import.record` of projects that spell their files otherwise than the repository: an fsproj
/// and a csproj naming a source, resources, a key file, an `<Import>` and a project reference in
/// another case, a `.resx` naming a file and a source including a doc file in another case. The
/// description and the lock name the repository's files (`git ls-files`), the resources keep
/// msbuild's names, and the import log names every difference. msbuild accepts such a project
/// only where the file system ignores case (macOS, the Windows leg): ignored elsewhere.
[<TestFixture>]
type ``Import casing``() =
    inherit XakeTestBase("import-casing")

    let resx =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n"
        + "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>\n"
        + "  <resheader name=\"version\"><value>2.0</value></resheader>\n"
        + "  <data name=\"Greeting\" xml:space=\"preserve\"><value>Hello</value></data>\n"
        + "  <data name=\"Copy\" type=\"System.Resources.ResXFileRef, System.Windows.Forms\"><value>..\\Images\\Copy.TXT;System.String, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></data>\n"
        + "</root>\n"

    /// Every folder a test of the fixture writes: each test starts without the others' projects
    /// (a build takes every description it finds)
    let folders = [ "app"; "lib"; "core"; "tool"; "wpf"; "locks"; "out"; "run"; "obj" ]

    let files =
        [ "shared.props", "<Project>\n  <PropertyGroup><Deterministic>true</Deterministic></PropertyGroup>\n</Project>\n"
          "lib/Lib.fsproj",
            RecordKit.sdkProject "<TargetFramework>net8.0</TargetFramework>" "<Compile Include=\"Src\\A.FS\" />"
          "lib/src/A.fs", "module Lib.A\nlet x = 1\n"
          "app/App.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n"
            + "  <Import Project=\"..\\Shared.PROPS\" />\n"
            + "  <PropertyGroup><TargetFramework>net8.0</TargetFramework><EnableDefaultItems>false</EnableDefaultItems>"
            + "<SignAssembly>true</SignAssembly><AssemblyOriginatorKeyFile>Keys\\App.SNK</AssemblyOriginatorKeyFile></PropertyGroup>\n"
            + "  <ItemGroup>\n"
            + "    <Compile Include=\"Code\\Program.CS\" />\n"
            + "    <EmbeddedResource Include=\"Resources\\Strings.RESX\" />\n"
            + "    <EmbeddedResource Include=\"Data\\Logo.TXT\" />\n"
            + "    <ProjectReference Include=\"..\\LIB\\lib.fsproj\" />\n"
            + "  </ItemGroup>\n</Project>\n"
          "app/code/Program.cs",
            "namespace App {\n  /// <include file='Docs\\Common.XML' path='doc/member[@name=\"C\"]/*'/>\n  public class C { }\n}\n"
          "app/code/docs/common.xml", "<doc><member name=\"C\"><summary>C</summary></member></doc>\n"
          "app/resources/Strings.resx", resx
          "app/images/copy.txt", "copied\n"
          "app/data/logo.txt", "logo\n"
          "app/keys/app.snk", "not a key: the design-time build does not read it\n" ]

    [<Test; Category("Integration")>]
    member x.``the description and the lock name the repository's files; names stay msbuild's; every difference is logged``() =
        // the probe: msbuild cannot evaluate these projects where case matters
        File.WriteAllText ("case-probe.txt", "")
        let ignoresCase = File.Exists "CASE-PROBE.TXT"
        File.Delete "case-probe.txt"
        if not ignoresCase then Assert.Ignore "the file system here does not ignore case: msbuild cannot evaluate a miscased project"
        if Directory.Exists ".git" then Directory.Delete (".git", true)
        RecordKit.write folders files
        let here = (Directory.GetCurrentDirectory ()).Replace ('\\', '/')
        CasingKit.git here [ "init"; "-q" ] |> ignore
        CasingKit.git here ([ "add"; "--" ] @ List.map fst files) |> ignore
        let tracked = (CasingKit.git here [ "ls-files"; "-z" ]).Split ([| '\000' |], StringSplitOptions.RemoveEmptyEntries) |> set

        let imported = RecordKit.import x.TestOptions "import-casing" RecordKit.recordDefault [ "app/App.csproj"; "LIB/lib.fsproj" ]

        // the descriptions: every file the repository's
        let app = Description.read "app/xake.project.json"
        let lib = Description.read "lib/xake.project.json"
        Assert.That(lib.Sources, Is.EqualTo [ "src/A.fs" ])
        Assert.That(lib.Name, Is.EqualTo "Lib")
        Assert.That(app.Sources, Is.EqualTo [ "code/Program.cs" ])
        Assert.That(app.ProjectRefs, Is.EqualTo [ "Lib" ])
        Assert.That(app.Options.KeyFile, Is.EqualTo (Some "keys/app.snk"))
        // the resources keep the names msbuild gives them from the project's spelling
        Assert.That(app.Resources |> List.map (fun r -> r.File, r.Name),
                    Is.EquivalentTo [ "resources/Strings.resx", Some "App.Resources.Strings.resources"; "data/logo.txt", Some "App.Data.Logo.TXT" ])
        for file in [ "app/App.csproj"; "lib/Lib.fsproj" ] @ (lib.Sources |> List.map ((+) "lib/"))
                    @ (app.Sources |> List.map ((+) "app/")) @ (app.Resources |> List.map (fun r -> "app/" + r.File)) @ [ "app/" + app.Options.KeyFile.Value ] do
            Assert.That(tracked.Contains file, Is.True, sprintf "the description names '%s', which git does not track" file)

        // the locks: every file of the repository they name is one git tracks
        for name in [ "App"; "Lib" ] do
            let lock = File.ReadAllText (RecordKit.lockOf name "Release")
            let named =
                [ for m in Text.RegularExpressions.Regex.Matches (lock, "\\$\\(ProjectRoot\\)/([^\",\\\\\\s]+)") -> m.Groups.[1].Value.TrimEnd '/' ]
                |> List.filter (fun p -> not (p.Contains "/obj/" || p.StartsWith "obj/" || p.StartsWith "out/" || p.StartsWith "locks/" || p.StartsWith ".packages/"))
                |> List.distinct
            Assert.That(named, Is.Not.Empty)
            for p in named do
                // a file git tracks, or a folder of one (the compiler's directory)
                Assert.That(tracked.Contains p || tracked |> Set.exists (fun t -> t.StartsWith (p + "/")), Is.True,
                            sprintf "the lock of %s names '%s', which git does not track" name p)
        let appLock = File.ReadAllText (RecordKit.lockOf "App" "Release")
        // the resources: msbuild's names; the resx's file reference recorded as the repository's file
        Assert.That(appLock, Does.Contain "\"/resource:$(ProjectRoot)/app/data/logo.txt,App.Data.Logo.TXT\"")
        Assert.That(appLock, Does.Contain "App.Resources.Strings.resources")
        Assert.That(appLock, Does.Contain "\"Ref\": \"..\\\\Images\\\\Copy.TXT\", \"Path\": \"$(ProjectRoot)/app/images/copy.txt\"")
        Assert.That(appLock, Does.Contain "\"/keyfile:$(ProjectRoot)/app/keys/app.snk\"")
        // the referenced project's output by the repository's name
        Assert.That(appLock, Does.Contain "\"Path\": \"$(ProjectRoot)/out/Release/net8.0/Lib/Lib.dll\"")
        Assert.That((File.ReadAllText "app/resources/Strings.resx"), Is.EqualTo resx)

        // the import log (`Imported.Casing`, traced at Info): every difference, once, with both spellings
        let lines = imported |> List.collect (fun i -> i.Casing)
        let expected =
            [ "'LIB/lib.fsproj' is 'lib/Lib.fsproj'"
              "'lib/Src/A.FS' is 'lib/src/A.fs'"
              "'Shared.PROPS' is 'shared.props'"
              "'app/Code/Program.CS' is 'app/code/Program.cs'"
              "'app/Resources/Strings.RESX' is 'app/resources/Strings.resx'"
              "'app/Data/Logo.TXT' is 'app/data/logo.txt'"
              "'app/Keys/App.SNK' is 'app/keys/app.snk'"
              "'app/Images/Copy.TXT' (ResXFileRef '..\\Images\\Copy.TXT' in 'app/resources/Strings.resx') is 'app/images/copy.txt'"
              "'app/code/Docs/Common.XML' (<include file='Docs\\Common.XML'> in 'app/code/Program.cs') is 'app/code/docs/common.xml'" ]
        for e in expected do
            Assert.That(lines |> List.exists (fun l -> l.StartsWith e), Is.True, sprintf "no casing line '%s' in:\n%s" e (String.concat "\n" lines))
        Assert.That(List.distinct lines, Has.Length.EqualTo expected.Length)

    [<Test; Category("Integration")>]
    member x.``an application's deps.json names a miscased project reference's file as the build writes it, and its run folder builds``() =
        File.WriteAllText ("case-probe.txt", "")
        let ignoresCase = File.Exists "CASE-PROBE.TXT"
        File.Delete "case-probe.txt"
        if not ignoresCase then Assert.Ignore "the file system here does not ignore case: msbuild cannot evaluate a miscased project"
        if Directory.Exists ".git" then Directory.Delete (".git", true)
        let appFiles =
            [ "core/Core.csproj", RecordKit.sdkProject "<TargetFramework>net8.0</TargetFramework>" ""
              "core/Greeter.cs", "namespace Core { public static class Greeter { public static string Hello () => \"hello from core\"; } }\n"
              "tool/Tool.csproj",
                RecordKit.sdkProject "<TargetFramework>net8.0</TargetFramework><OutputType>Exe</OutputType>" "<ProjectReference Include=\"..\\CORE\\core.csproj\" />"
              "tool/Program.cs", "public static class Program { public static void Main () => System.Console.WriteLine (Core.Greeter.Hello ()); }\n" ]
        RecordKit.write folders appFiles
        let here = (Directory.GetCurrentDirectory ()).Replace ('\\', '/')
        CasingKit.git here [ "init"; "-q" ] |> ignore
        CasingKit.git here ([ "add"; "--" ] @ List.map fst appFiles) |> ignore

        RecordKit.import x.TestOptions "import-casing-app" RecordKit.recordDefault [ "tool/Tool.csproj"; "core/Core.csproj" ] |> ignore
        let deps = File.ReadAllText "locks/Tool/Release.files/net8.0/app/Tool.deps.json"
        // the runtime file as the build writes it (msbuild's reference names `core.dll`); the
        // library keeps msbuild's name (SDK 10 writes the project's version there, no token)
        Assert.That(deps, Does.Contain "\"Core.dll\": {")
        Assert.That(deps, Does.Not.Contain "\"core.dll\"")
        let lock = File.ReadAllText (RecordKit.lockOf "Tool" "Release")
        Assert.That(lock, Does.Contain "\"To\": \"Core.dll\"")

        // the replay: the outputs from the locks, the run folder, and the application runs
        RecordKit.build x.TestOptions "build-casing-app" "Release"
        RecordKit.deploy x.TestOptions "deploy-casing-app" "Release" RunFolder.Copy
        let folder = Path.GetFullPath "run/Release/net8.0/Tool"
        let placed = Directory.GetFiles folder |> Array.map Path.GetFileName |> set
        Assert.That(placed.Contains "Core.dll", Is.True, sprintf "the run folder holds %A" placed)
        Assert.That(File.ReadAllText (Path.Combine (folder, "Tool.deps.json")), Does.Contain "\"Core.dll\": {")
        let psi = ProcessStartInfo ("dotnet", "Tool.dll")
        psi.WorkingDirectory <- folder
        psi.RedirectStandardOutput <- true
        psi.UseShellExecute <- false
        use p = Process.Start psi
        let out = p.StandardOutput.ReadToEnd ()
        p.WaitForExit ()
        Assert.That(out.Trim (), Is.EqualTo "hello from core")

    [<Test; Category("Integration")>]
    member x.``a WPF project's miscased XAML and resource are recorded as the repository's files, the BAML names as msbuild's``() =
        File.WriteAllText ("case-probe.txt", "")
        let ignoresCase = File.Exists "CASE-PROBE.TXT"
        File.Delete "case-probe.txt"
        if not ignoresCase then Assert.Ignore "the file system here does not ignore case: msbuild cannot evaluate a miscased project"
        if Directory.Exists ".git" then Directory.Delete (".git", true)
        let presentation = "xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\""
        let wpfFiles =
            [ "wpf/wpf.csproj",
                RecordKit.sdkProject
                    "<TargetFramework>net8.0-windows</TargetFramework><UseWPF>true</UseWPF><EnableWindowsTargeting>true</EnableWindowsTargeting><EnableDefaultPageItems>false</EnableDefaultPageItems>"
                    "<Page Include=\"Views\\Brushes.XAML\" /><Resource Include=\"Data.TXT\" />"
              "wpf/views/Brushes.xaml", sprintf "<ResourceDictionary %s>\n  <SolidColorBrush x:Key=\"accent\" Color=\"Red\" />\n</ResourceDictionary>\n" presentation
              "wpf/data.txt", "a resource\n"
              "wpf/Code.cs", "namespace Wpf { public class Code { } }\n" ]
        RecordKit.write folders wpfFiles
        let here = (Directory.GetCurrentDirectory ()).Replace ('\\', '/')
        CasingKit.git here [ "init"; "-q" ] |> ignore
        CasingKit.git here ([ "add"; "--" ] @ List.map fst wpfFiles) |> ignore

        let imported = RecordKit.import x.TestOptions "import-casing-wpf" RecordKit.recordDefault [ "wpf/wpf.csproj" ]
        let lock = File.ReadAllText (RecordKit.lockOf "wpf" "Release")
        // the markup inputs (Evaluation.Imports): the repository's files
        Assert.That(lock, Does.Contain "\"$(ProjectRoot)/wpf/views/Brushes.xaml\"")
        Assert.That(lock, Does.Contain "\"$(ProjectRoot)/wpf/data.txt\"")
        Assert.That(lock, Does.Not.Contain "Views/Brushes.XAML")
        Assert.That(lock, Does.Not.Contain "/wpf/Data.TXT")
        let lines = imported |> List.collect (fun i -> i.Casing)
        for e in [ "'wpf/Views/Brushes.XAML' is 'wpf/views/Brushes.xaml'"; "'wpf/Data.TXT' is 'wpf/data.txt'" ] do
            Assert.That(lines |> List.exists (fun l -> l.StartsWith e), Is.True, sprintf "no casing line '%s' in:\n%s" e (String.concat "\n" lines))
        // the g.resources is msbuild's, bytes and names: its BAML key from the project's spelling
        let resources = File.ReadAllBytes "locks/wpf/Release.files/net8.0-windows/wpf.g.resources"
        let keys =
            use reader = new System.Resources.ResourceReader (new MemoryStream (resources))
            [ for e in reader |> Seq.cast<System.Collections.DictionaryEntry> -> string e.Key ]
        Assert.That(keys, Does.Contain "views/brushes.baml")
        Assert.That(keys, Does.Contain "data.txt")
        RecordKit.build x.TestOptions "build-casing-wpf" "Release"
        let _, _, embedded = RecordKit.metadata "out/Release/net8.0-windows/wpf/wpf.dll"
        Assert.That(embedded, Does.Contain "wpf.g.resources")

    [<Test; Category("Integration")>]
    member x.``a project in a submodule of a sibling repository, reached through a declared root, takes that submodule's spelling``() =
        File.WriteAllText ("case-probe.txt", "")
        let ignoresCase = File.Exists "CASE-PROBE.TXT"
        File.Delete "case-probe.txt"
        if not ignoresCase then Assert.Ignore "the file system here does not ignore case: msbuild cannot evaluate a miscased project"
        if Directory.Exists ".git" then Directory.Delete (".git", true)
        let here = (Directory.GetCurrentDirectory ()).Replace ('\\', '/')
        for d in [ "script"; "sibling"; "subsrc" ] do if Directory.Exists d then Directory.Delete (d, true)
        let commit dir = CasingKit.git dir [ "-c"; "user.name=t"; "-c"; "user.email=t@t"; "commit"; "-q"; "-m"; "x" ] |> ignore
        // the script's repository: the project root, nothing of the project in it
        Directory.CreateDirectory "script" |> ignore
        File.WriteAllText ("script/build.fsx", "// the script\n")
        CasingKit.git (here + "/script") [ "init"; "-q" ] |> ignore
        CasingKit.git (here + "/script") [ "add"; "build.fsx" ] |> ignore
        // the submodule's repository: the project, its analyzer config, a source including a doc file
        let sub = here + "/subsrc"
        let subFiles =
            [ "sub.csproj",
                RecordKit.sdkProject "<TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems>"
                    "<Compile Include=\"Code\\X.CS\" /><Compile Include=\"code\\deep\\Y.cs\" />"
              ".editorConfig", "root = true\n[*.cs]\nindent_size = 4\n"
              "code/X.cs", "namespace Sub {\n  /// <include file='../Docs/Common.XML' path='doc/member[@name=\"X\"]/*'/>\n  public class X { }\n}\n"
              "docs/common.xml", "<doc><member name=\"X\"><summary>X</summary></member><member name=\"Y\"><summary>Y</summary></member></doc>\n"
              // csc's second lookup: not beside the source, under the project folder
              "code/deep/Y.cs", "namespace Sub {\n  /// <include file='Docs/Common.XML' path='doc/member[@name=\"Y\"]/*'/>\n  public class Y { }\n}\n"
              // a resx naming an image in another case
              "resources/Strings.resx",
                "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n"
                + "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>\n"
                + "  <resheader name=\"version\"><value>2.0</value></resheader>\n"
                + "  <data name=\"Logo\" type=\"System.Resources.ResXFileRef, System.Windows.Forms\"><value>..\\Images\\Logo.PNG;System.Byte[], mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089</value></data>\n"
                + "</root>\n"
              "images/logo.png", "not really a png\n" ]
        for path, text in subFiles do
            Directory.CreateDirectory (Path.GetDirectoryName (Path.Combine (sub, path))) |> ignore
            File.WriteAllText (Path.Combine (sub, path), text)
        CasingKit.git sub [ "init"; "-q" ] |> ignore
        CasingKit.git sub ([ "add"; "--" ] @ List.map fst subFiles) |> ignore
        commit sub
        // the sibling repository: its Directory.Build.props spelled otherwise, the submodule
        let sibling = here + "/sibling"
        Directory.CreateDirectory sibling |> ignore
        File.WriteAllText (sibling + "/Directory.build.props", "<Project>\n  <PropertyGroup><Deterministic>true</Deterministic></PropertyGroup>\n</Project>\n")
        CasingKit.git sibling [ "init"; "-q" ] |> ignore
        CasingKit.git sibling [ "add"; "Directory.build.props" ] |> ignore
        CasingKit.git sibling [ "-c"; "protocol.file.allow=always"; "submodule"; "add"; "-q"; sub; "ext/Sub" ] |> ignore
        commit sibling

        let options = { x.TestOptions with ProjectRoot = here + "/script" }
        let record = { RecordKit.recordDefault with Roots = [ "$(Sib)", sibling ] }
        let imported = RecordKit.import options "import-casing-sibling" record [ sibling + "/ext/Sub/sub.csproj" ]
        let lock = File.ReadAllText "script/locks/sub/Release.json"
        // the msbuild import, the analyzer config, the source: as the repositories that hold them have them
        Assert.That(lock, Does.Contain "\"$(Sib)/Directory.build.props\"")
        Assert.That(lock, Does.Not.Contain "Directory.Build.props")
        Assert.That(lock, Does.Contain "/analyzerconfig:$(Sib)/ext/Sub/.editorConfig")
        Assert.That(lock, Does.Contain "$(Sib)/ext/Sub/code/X.cs")
        Assert.That(File.ReadAllText (sibling + "/ext/Sub/xake.project.json"), Does.Contain "\"code/X.cs\"")
        let lines = imported |> List.collect (fun i -> i.Casing)
        let has (prefix: string) = lines |> List.exists (fun l -> l.Contains prefix)
        Assert.That(has "Directory.Build.props' is '", Is.True, String.concat "\n" lines)
        Assert.That(has "/ext/Sub/.editorconfig' is '", Is.True, String.concat "\n" lines)
        Assert.That(has "/ext/Sub/Code/X.CS' is '", Is.True, String.concat "\n" lines)
        Assert.That(has "(<include file='../Docs/Common.XML'> in ", Is.True, String.concat "\n" lines)
        // found through the project folder only (csc's second lookup)
        Assert.That(has "(<include file='Docs/Common.XML'> in ", Is.True, String.concat "\n" lines)
        // the resx's image: recorded under the declared root's token, with its hash; the line says so
        let sha =
            use algo = Security.Cryptography.SHA256.Create ()
            algo.ComputeHash (File.ReadAllBytes (sibling + "/ext/Sub/images/logo.png")) |> Array.map (sprintf "%02x") |> String.concat ""
        Assert.That(lock, Does.Contain (sprintf "\"Ref\": \"..\\\\Images\\\\Logo.PNG\", \"Path\": \"$(Sib)/ext/Sub/images/logo.png\", \"Sha256\": \"%s\"" sha))
        Assert.That(has "(ResXFileRef '..\\Images\\Logo.PNG' in ", Is.True, String.concat "\n" lines)
        Assert.That(lines |> List.exists (fun l -> l.Contains "Logo.PNG" && l.Contains "the lock records that file for it (ResxFiles)"), Is.True, String.concat "\n" lines)
        // the project builds from its lock, its resources compiled from the recorded image
        let compile = recipe {
            let! cfg = getRuleMatch "cfg"
            let! fwk = getRuleMatch "fwk"
            let! name = getRuleMatch "name"
            do! Locks.compileAsWith [ "$(Sib)", sibling ] (RecordKit.lockOf name cfg) fwk [] }
        RecordKit.run options "build-casing-sibling" [ "out/(cfg:*)/(fwk:*)/(name:*)/*.dll" ..> compile ] [ "out/Release/net8.0/sub/sub.dll" ]
        let _, _, embedded = RecordKit.metadata "script/out/Release/net8.0/sub/sub.dll"
        Assert.That(embedded, Does.Contain "sub.resources.Strings.resources")
