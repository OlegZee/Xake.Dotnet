namespace Tests

open System
open System.IO
open System.Diagnostics
open NUnit.Framework

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

    let ok = function Ok i -> i | Error (e: string) -> failwith e


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
            | Ok _ -> Assert.Fail "an index outside a repository"
            | Error e -> Assert.That(e, Does.Contain "git")
            Assert.That(Casing.Resolver.Unchanged.Path (dir + "/Foo.cs"), Is.EqualTo (dir + "/Foo.cs"))
            Assert.That(Casing.Resolver.Unchanged.Differences, Is.Empty)
        finally Directory.Delete (dir, true)

    [<Test>]
    member x.``a resolver keeps a relative path relative, and notes each difference once``() =
        let root = CasingKit.repository "casing-rel" [ "lib/Strings.resx", ""; "app/Key.snk", "" ]
        try
            let r = Casing.Resolver (Some (Casing.read root |> CasingKit.ok))
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
