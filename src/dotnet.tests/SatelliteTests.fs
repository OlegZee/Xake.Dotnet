namespace Tests

open System
open System.IO
open System.Diagnostics
open NUnit.Framework

open Xake
open Xake.Tasks
open Xake.Dotnet

/// The pure core of a satellite's compile (`Satellite`): the attribute rule table, the manifest
/// names, the framework, the references, the switches, the culture.
[<TestFixture>]
type ``Satellite core``() =

    let template (attributes: (string * string) list) : Satellite.Template =
        { Path = "/x/Lib.dll"; Name = "Lib"; Version = Version (1, 2, 3, 0); PublicKey = [||]; Framework = Some "net8.0"; Optimized = true
          Attributes = attributes |> List.map (fun (n, v) -> "System.Reflection." + n + "Attribute", v)
          Resources = [ "Lib.Strings.resources" ] }

    let r name = "System.Reflection." + name + "Attribute"

    [<Test>]
    member __.``attributes absent: Copyright and Title blank, InformationalVersion the version``() =
        Assert.That(Satellite.attributesOf (template []) [],
            Is.EqualTo [ r "AssemblyCopyright", " "; r "AssemblyInformationalVersion", "1.2.3.0"; r "AssemblyTitle", " "; r "AssemblyVersion", "1.2.3.0" ])

    [<Test>]
    member __.``attributes present: each as stated, in GetAssemblyAttributes' order``() =
        let stated =
            [ "AssemblyTitle", "T"; "AssemblyProduct", "P"; "AssemblyCompany", "C"; "AssemblyCopyright", "(c)"
              "AssemblyDescription", "D"; "AssemblyFileVersion", "1.2.3.4"; "AssemblyInformationalVersion", "1.2.3-beta"
              "AssemblyTrademark", "TM"; "AssemblyConfiguration", "Release" ]
        Assert.That(Satellite.attributesOf (template stated) [],
            Is.EqualTo [ r "AssemblyCompany", "C"; r "AssemblyCopyright", "(c)"; r "AssemblyDescription", "D"; r "AssemblyFileVersion", "1.2.3.4"
                         r "AssemblyInformationalVersion", "1.2.3-beta"; r "AssemblyProduct", "P"; r "AssemblyTitle", "T"; r "AssemblyVersion", "1.2.3.0" ])

    [<Test>]
    member __.``attributes stated as empty strings are left out``() =
        let empty = [ for n in [ "AssemblyCompany"; "AssemblyCopyright"; "AssemblyDescription"; "AssemblyFileVersion"; "AssemblyInformationalVersion"; "AssemblyProduct"; "AssemblyTitle" ] -> n, "" ]
        Assert.That(Satellite.attributesOf (template empty) [], Is.EqualTo [ r "AssemblyVersion", "1.2.3.0" ])

    [<Test>]
    member __.``an explicit attribute replaces the read one, a new one comes after``() =
        let got = Satellite.attributesOf (template [ "AssemblyCompany", "Read" ]) [ "AssemblyCompany", "Given"; "AssemblyTrademarkAttribute", "TM"; "System.CLSCompliantAttribute", "x"; "AssemblyCopyright", "" ]
        Assert.That(got,
            Is.EqualTo [ r "AssemblyCompany", "Given"; r "AssemblyCopyright", ""; r "AssemblyInformationalVersion", "1.2.3.0"; r "AssemblyTitle", " "; r "AssemblyVersion", "1.2.3.0"
                         r "AssemblyTrademark", "TM"; "System.CLSCompliantAttribute", "x" ])

    [<Test>]
    member __.``manifest names as the brief's table gives them``() =
        let root = Path.GetFullPath "translations-names"
        let named dynamic (file: string) =
            Satellite.manifestName "ja" (Impl.makeResourceName { Prefix = Some "Lib"; DynamicPrefix = dynamic } (Some (root </> "ja")) (root </> "ja" </> file))
        Assert.That(named true "Strings.resx", Is.EqualTo "Lib.Strings.ja.resources")
        Assert.That(named true ("Forms" </> "Main.resx"), Is.EqualTo "Lib.Forms.Main.ja.resources")
        Assert.That(named false ("Forms" </> "Main.resx"), Is.EqualTo "Lib.Main.ja.resources")
        Assert.That(named true "Strings.ja.resx", Is.EqualTo "Lib.Strings.ja.resources")
        Assert.That(named true "Text.restext", Is.EqualTo "Lib.Text.ja.resources")
        Assert.That(named true "Logo.png", Is.EqualTo "Lib.Logo.png")
        Assert.That(named true "Logo.ja.png", Is.EqualTo "Lib.Logo.png")
        let e = Assert.Throws<exn>(fun () -> named true "Strings.zh-CN.resx" |> ignore)
        Assert.That(e.Message, Does.Contain "of the culture 'zh-CN', not of the satellite's 'ja'")

    [<Test>]
    member __.``frameworks from the TargetFrameworkAttribute``() =
        Assert.That(Satellite.frameworkOf ".NETCoreApp,Version=v8.0", Is.EqualTo (Some "net8.0"))
        Assert.That(Satellite.frameworkOf ".NETFramework,Version=v4.7.2,Profile=Client", Is.EqualTo (Some "net472"))
        Assert.That(Satellite.frameworkOf ".NETStandard,Version=v2.0", Is.EqualTo (Some "netstandard2.0"))
        Assert.That(Satellite.frameworkOf "Silverlight,Version=v5.0", Is.EqualTo None)
        Assert.That(Satellite.frameworkOf "", Is.EqualTo None)

    [<Test>]
    member __.``a targetfwk is checked against the template's``() =
        let t = template []
        Assert.That(SatelliteTask.frameworkOf "s" t null, Is.EqualTo "net8.0")
        Assert.That(SatelliteTask.frameworkOf "s" t "net8.0", Is.EqualTo "net8.0")
        let e = Assert.Throws<exn>(fun () -> SatelliteTask.frameworkOf "s" t "net472" |> ignore)
        Assert.That(e.Message, Does.Contain "targetfwk 'net472' differs from the framework of 'Lib.dll' (net8.0)")
        Assert.That(SatelliteTask.frameworkOf "s" { t with Framework = None } "net472", Is.EqualTo "net472")
        let e = Assert.Throws<exn>(fun () -> SatelliteTask.frameworkOf "s" { t with Framework = None } "" |> ignore)
        Assert.That(e.Message, Does.Contain "names no target framework")

    [<Test>]
    member __.``references keep mscorlib, netstandard and System.Runtime; none fails``() =
        let refs (paths: string list) : Reference list = [ for p in paths -> { Path = p; Sha256 = ""; Alias = "" } ]
        let all = refs [ "/r/Microsoft.CSharp.dll"; "/r/mscorlib.dll"; "/r/netstandard.dll"; "/r/System.dll"; "/r/System.Runtime.dll"; "/r/System.Runtime.Loader.dll" ]
        Assert.That(Satellite.references "s" all |> List.map (fun r -> r.Path), Is.EqualTo [ "/r/mscorlib.dll"; "/r/netstandard.dll"; "/r/System.Runtime.dll" ])
        let e = Assert.Throws<exn>(fun () -> Satellite.references "s" (refs [ "/r/System.dll" ]) |> ignore)
        Assert.That(e.Message, Does.Contain "none of mscorlib, netstandard, System.Runtime among the references")

    [<Test>]
    member __.``options keep the switches the SDK passes on``() =
        let main = [ "/nostdlib+"; "/keyfile:/k.snk"; "/publicsign+"; "/optimize+"; "/deterministic+"; "/warn:8"; "/nowarn:1701"; "/warnaserror+"
                     "/warnaserror+:NU1605"; "/pathmap:/a=/b"; "/features:strict"; "/langversion:latest"; "/define:X"; "a.cs" ]
        Assert.That(Satellite.options main,
            Is.EqualTo [ "/noconfig"; "/nostdlib+"; "/keyfile:/k.snk"; "/publicsign+"; "/optimize+"; "/deterministic+"; "/warn:8"
                         "/warnaserror+:NU1605"; "/pathmap:/a=/b"; "/features:strict"; "/target:library" ])

    [<Test>]
    member __.``the culture must be spelled as .NET spells it``() =
        Satellite.checkCulture "s" "ja"
        Satellite.checkCulture "s" "zh-CN"
        Satellite.checkCulture "s" "zh-Hans"
        Assert.That(Satellite.isCulture "zh-cn", Is.True)
        Assert.That(Satellite.isCulture "v2", Is.False)
        let e = Assert.Throws<exn>(fun () -> Satellite.checkCulture "s" "zh-cn")
        Assert.That(e.Message, Does.Contain "the culture folder 'zh-cn' is spelled 'zh-CN' by .NET")
        let e = Assert.Throws<exn>(fun () -> Satellite.checkCulture "s" "xx-nowhere")
        Assert.That(e.Message, Does.Contain "is not a culture .NET knows")


/// A load context that reads an assembly and its satellites (`<dir>/<culture>/<name>.dll`) from
/// their bytes: nothing is cached by path, so the files can change between tests.
type SatelliteBytesContext (dir: string) =
    inherit Runtime.Loader.AssemblyLoadContext (true)
    member this.LoadMain (path: string) = this.LoadFromStream (new MemoryStream (File.ReadAllBytes path))
    override this.Load (name: Reflection.AssemblyName) =
        let satellite = Path.Combine (dir, (if isNull name.CultureName then "" else name.CultureName), name.Name + ".dll")
        if not (String.IsNullOrEmpty name.CultureName) && File.Exists satellite then this.LoadFromStream (new MemoryStream (File.ReadAllBytes satellite))
        else null

/// `satellite { }` against `dotnet build` (SDK 10): a signed library (net8.0 and net472) whose
/// own `Strings.ja.resx`/`Text.ja.restext` give msbuild's satellite, and the same translations
/// kept outside it (`translations/ja/`) given to `satellite { }` over the built main assembly.
[<TestFixture>]
type ``satellite task``() =
    inherit XakeTestBase("satellite")

    let resx (entries: (string * string) list) =
        "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n<root>\n"
        + "  <resheader name=\"resmimetype\"><value>text/microsoft-resx</value></resheader>\n"
        + "  <resheader name=\"version\"><value>2.0</value></resheader>\n"
        + (entries |> List.map (fun (k, v) -> sprintf "  <data name=\"%s\" xml:space=\"preserve\"><value>%s</value></data>\n" k v) |> String.concat "")
        + "</root>\n"

    /// A `.restext` with every rule msbuild's reader has: comments, `[strings]`, spaces around
    /// `=`, escapes, a skipped indented line, an empty value, a repeated name, CRLF and LF.
    let neutralText =
        "# a comment\n; another\n[strings]\nHello = Hi there\nTab=a\\tb\r\nNewline=line1\\nline2\nBack=c:\\\\path\nQuote=say \\\"hi\\\"\n"
        + "Uni=\\u00e9t\\u00e9\nTrailing=keep   \nTwoSpaces  =  x\n indented=ignored\n\tTabbed=ignored\nEmpty=\nDup=first\nDup=second\nUnicode=日本\nLast=no newline"
    let jaText = "Hello = Konnichiwa\nUni=\\u65e5\\u672c\n"

    let toFixedLen len (b: byte[]) =
        if b.Length = len then b
        elif b.Length > len then b.[b.Length - len ..]
        else Array.append (Array.zeroCreate (len - b.Length)) b

    /// A strong-name key pair as a `.snk` (PRIVATEKEYBLOB).
    let snk () =
        use rsa = Security.Cryptography.RSA.Create 1024
        let key = rsa.ExportParameters true
        let byteLen = key.Modulus.Length
        let block len (b: byte[]) = toFixedLen len b |> Array.rev
        Array.concat
            [ [| 0x07uy; 0x02uy; 0x00uy; 0x00uy; 0x00uy; 0x24uy; 0x00uy; 0x00uy |]
              Text.Encoding.ASCII.GetBytes "RSA2"
              BitConverter.GetBytes (byteLen * 8)
              block 4 key.Exponent
              block byteLen key.Modulus
              block (byteLen / 2) key.P
              block (byteLen / 2) key.Q
              block (byteLen / 2) key.DP
              block (byteLen / 2) key.DQ
              block (byteLen / 2) key.InverseQ
              block byteLen key.D ]

    /// Runs `dotnet <args>` in `workDir` with the msbuild of the SDK `global.json` picks there,
    /// not the test host's; fails with both streams on a non-zero exit.
    let runDotnet (workDir: string) (args: string list) =
        let psi = ProcessStartInfo ("dotnet")
        psi.Environment.["MSBUILDDISABLENODEREUSE"] <- "1"
        for n in [ "MSBuildSDKsPath"; "MSBuildExtensionsPath"; "MSBUILD_EXE_PATH"; "MSBuildLoadMicrosoftTargetsReadOnly" ] do
            psi.Environment.Remove n |> ignore
        psi.WorkingDirectory <- workDir
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.UseShellExecute <- false
        for a in args do psi.ArgumentList.Add a
        use p = Process.Start psi
        let out = p.StandardOutput.ReadToEnd ()
        let err = p.StandardError.ReadToEnd ()
        p.WaitForExit ()
        if p.ExitCode <> 0 then
            failwithf "'dotnet %s' failed (exit %d):\n%s\n%s" (String.Join (" ", args)) p.ExitCode out err

    let sdk10 () =
        let p = Process.Start (ProcessStartInfo ("dotnet", "--list-sdks", RedirectStandardOutput = true, UseShellExecute = false))
        let installed = p.StandardOutput.ReadToEnd ()
        p.WaitForExit ()
        installed.Split '\n' |> Array.exists (fun l -> l.StartsWith "10.")

    /// The fixture, built once: `proj/lib` (signed with `key.snk`; `Strings.resx`, `Text.restext`
    /// and their `ja` ones) built by `dotnet build -c Release` and `-c Debug`, the translations in
    /// `translations/ja/`, another key in `other.snk`, an unsigned template `plain/lib.dll`.
    let fixture =
        lazy (
            if not (sdk10 ()) then None else
            for d in [ "proj"; "translations"; "plain" ] do if Directory.Exists d then Directory.Delete (d, true)
            Directory.CreateDirectory "proj/lib" |> ignore
            Directory.CreateDirectory "translations/ja" |> ignore
            Directory.CreateDirectory "plain" |> ignore
            File.WriteAllText ("global.json", "{ \"sdk\": { \"version\": \"10.0.100\", \"rollForward\": \"latestFeature\" } }\n")
            File.WriteAllBytes ("key.snk", snk ())
            File.WriteAllBytes ("other.snk", snk ())
            File.WriteAllText ("proj/lib/lib.csproj",
                "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n"
                + "    <TargetFrameworks>net472;net8.0</TargetFrameworks><Version>1.2.3-beta.1</Version><Company>Co</Company>\n"
                + "    <Product>Lib \"ü\"</Product><Deterministic>true</Deterministic><NuGetAudit>false</NuGetAudit>\n"
                + "    <SignAssembly>true</SignAssembly><AssemblyOriginatorKeyFile>../../key.snk</AssemblyOriginatorKeyFile>\n"
                + "  </PropertyGroup>\n  <ItemGroup>\n    <EmbeddedResource Include=\"Text.restext\" />\n    <EmbeddedResource Include=\"Text.ja.restext\" />\n  </ItemGroup>\n</Project>\n")
            File.WriteAllText ("proj/lib/C.cs", "namespace Lib { public class C { } }\n")
            File.WriteAllText ("proj/lib/Strings.resx", resx [ "Hello", "Hello" ])
            File.WriteAllText ("proj/lib/Strings.ja.resx", resx [ "Hello", "Konnichiwa" ])
            File.WriteAllText ("proj/lib/Text.restext", neutralText)
            File.WriteAllText ("proj/lib/Text.ja.restext", jaText)
            File.WriteAllText ("translations/ja/Strings.resx", resx [ "Hello", "Konnichiwa" ])
            File.WriteAllText ("translations/ja/Text.restext", jaText)
            runDotnet "proj/lib" [ "build"; "-c"; "Release"; "-nologo"; "-v:q" ]
            runDotnet "proj/lib" [ "build"; "-c"; "Debug"; "-nologo"; "-v:q" ]
            // an unsigned template, compiled by hand: its framework from the attribute
            File.WriteAllText ("plain/A.cs", "[assembly: System.Runtime.Versioning.TargetFramework(\".NETCoreApp,Version=v8.0\")]\nnamespace Lib { public class C { } }\n")
            File.WriteAllText ("plain/Strings.resx", resx [ "Hello", "Hello" ])
            Some ())

    member private x.Fixture () =
        match fixture.Force () with
        | Some () -> ()
        | None -> Assert.Ignore "no SDK 10 here"

    member private x.Template fwk = sprintf "proj/lib/bin/Release/%s/lib.dll" fwk

    /// Builds `targets` (`<set>/<fwk>/<culture>/<asm>.resources.dll`) with `make set fwk culture`;
    /// the log is `<label>.log`.
    member private x.Build (label: string) (targets: string list) (make: string -> string -> string -> Recipe<ExecContext, unit>) =
        do xake { x.TestOptions with FileLog = label + ".log"; FileLogLevel = Verbosity.Loud; ThrowOnError = true } {
            want targets
            rules [
                "plain/lib.dll" ..> csc { targetfwk "net8.0"; src (!! "plain/A.cs"); resources (resourceset { prefix "lib"; basedir "plain"; includes "Strings.resx" }) }
                "(set:*)/(fwk:*)/(culture:*)/(asm:*).resources.dll" ..> recipe {
                    let! set = getRuleMatch "set"
                    let! fwk = getRuleMatch "fwk"
                    let! culture = getRuleMatch "culture"
                    do! make set fwk culture } ]
        }

    member private x.Fails (label: string) (targets: string list) make : string =
        let e = Assert.Throws<XakeException>(fun () -> x.Build label targets make)
        e.Data0

    /// The usual satellite: the template of `fwk`, the key, the translations of `culture`.
    member private x.Usual fwk culture =
        satellite {
            template (x.Template fwk)
            keyfile "key.snk"
            resources (resourceset { prefix "lib"; basedir $"translations/{culture}"; includes "**/*.resx" })
            resources (resourceset { prefix "lib"; basedir $"translations/{culture}"; includes "**/*.restext" })
        }

    [<Test; Category("Integration")>]
    member x.``a satellite of an external translation is dotnet build's, byte for byte``() =
        x.Fixture ()
        x.Build "usual" [ "out/net8.0/ja/lib.resources.dll"; "out/net472/ja/lib.resources.dll" ] (fun _ fwk culture -> x.Usual fwk culture)
        for fwk in [ "net8.0"; "net472" ] do
            let ours = sprintf "out/%s/ja/lib.resources.dll" fwk
            Assert.That(File.ReadAllBytes ours, Is.EqualTo (File.ReadAllBytes (sprintf "proj/lib/bin/Release/%s/ja/lib.resources.dll" fwk)), fwk)
            let main = Reflection.AssemblyName.GetAssemblyName (Path.GetFullPath (x.Template fwk))
            let sat = Reflection.AssemblyName.GetAssemblyName (Path.GetFullPath ours)
            Assert.That((sat.Name, sat.Version, sat.CultureName, sat.GetPublicKeyToken ()), Is.EqualTo (("lib.resources", main.Version, "ja", main.GetPublicKeyToken ())), fwk)
            Assert.That(main.GetPublicKeyToken (), Is.Not.Empty)
        let context = SatelliteBytesContext (Path.GetFullPath "out/net8.0")
        try
            let asm = context.LoadMain (x.Template "net8.0")
            let ja = Globalization.CultureInfo "ja"
            Assert.That(Resources.ResourceManager("lib.Strings", asm).GetString ("Hello", ja), Is.EqualTo "Konnichiwa")
            Assert.That(Resources.ResourceManager("lib.Text", asm).GetString ("Uni", ja), Is.EqualTo "日本")
            Assert.That(Resources.ResourceManager("lib.Text", asm).GetString ("Newline", ja), Is.EqualTo "line1\nline2")
        finally context.Unload ()

    [<Test; Category("Integration")>]
    member x.``a Debug template gives dotnet build's Debug satellite: /optimize as the template was compiled``() =
        x.Fixture ()
        Assert.That((Satellite.readTemplate (x.Template "net8.0")).Optimized, Is.True)
        Assert.That((Satellite.readTemplate "proj/lib/bin/Debug/net8.0/lib.dll").Optimized, Is.False)
        x.Build "debug" [ "debug/net8.0/ja/lib.resources.dll"; "debug/net472/ja/lib.resources.dll" ] (fun _ fwk culture ->
            satellite {
                template (sprintf "proj/lib/bin/Debug/%s/lib.dll" fwk)
                keyfile "key.snk"
                resources (resourceset { prefix "lib"; basedir $"translations/{culture}"; includes "**/*.resx" })
                resources (resourceset { prefix "lib"; basedir $"translations/{culture}"; includes "**/*.restext" })
            })
        for fwk in [ "net8.0"; "net472" ] do
            Assert.That(File.ReadAllBytes (sprintf "debug/%s/ja/lib.resources.dll" fwk),
                        Is.EqualTo (File.ReadAllBytes (sprintf "proj/lib/bin/Debug/%s/ja/lib.resources.dll" fwk)), fwk)

    [<Test; Category("Integration")>]
    member x.``a .restext compiles to dotnet build's .resources, in csc {} too``() =
        x.Fixture ()
        Assert.That(ResxFormat.compileBytes "proj/lib/Text.restext", Is.EqualTo (File.ReadAllBytes "proj/lib/obj/Release/net8.0/lib.Text.resources"))
        Assert.That(ResxFormat.compileBytes "proj/lib/Text.ja.restext", Is.EqualTo (File.ReadAllBytes "proj/lib/obj/Release/net8.0/lib.Text.ja.resources"))
        let entries = ResxFormat.readText "proj/lib/Text.restext" |> List.map (function ResxFormat.Text (n, v) -> n, v | e -> failwithf "%A" e)
        Assert.That(entries,
            Is.EqualTo [ "Hello", "Hi there"; "Tab", "a\tb"; "Newline", "line1\nline2"; "Back", "c:\\path"; "Quote", "say \"hi\""; "Uni", "été"
                         "Trailing", "keep   "; "TwoSpaces ", " x"; "Empty", ""; "Dup", "first"; "Unicode", "日本"; "Last", "no newline" ])
        // the neutral one in a csc {} compile: compiled under obj/xake/<name>/ and embedded
        do xake { x.TestOptions with FileLog = "restext-csc.log"; ThrowOnError = true } {
            want [ "restext/lib.dll" ]
            rules [ "restext/lib.dll" ..> csc { targetfwk "net8.0"; src (!! "proj/lib/C.cs"); resources (resourceset { prefix "lib"; basedir "proj/lib"; includes "Text.restext" }) } ]
        }
        Assert.That(File.ReadAllBytes "obj/xake/lib/lib.Text.resources", Is.EqualTo (File.ReadAllBytes "proj/lib/obj/Release/net8.0/lib.Text.resources"))
        Assert.That((Satellite.readTemplate "restext/lib.dll").Resources, Is.EqualTo [ "lib.Text.resources" ])

    [<Test; Category("Integration")>]
    member x.``an explicit attribute replaces the read one; quotes, newlines and non-ASCII compile``() =
        x.Fixture ()
        x.Build "attrs" [ "attrs/net8.0/ja/lib.resources.dll" ] (fun _ fwk culture ->
            satellite {
                template (x.Template fwk)
                keyfile "key.snk"
                resources (resourceset { prefix "lib"; basedir $"translations/{culture}"; includes "**/*.resx" })
                attribute "AssemblyCompany" "Other"
                attribute "AssemblyTrademark" "say \"hi\"\nnext line: 日本 ü"
            })
        let attrs = (Satellite.readTemplate "attrs/net8.0/ja/lib.resources.dll").Attributes
        let main = (Satellite.readTemplate (x.Template "net8.0")).Attributes
        let get (a: (string * string) list) n = a |> List.tryFind (fst >> (=) ("System.Reflection." + n)) |> Option.map snd
        Assert.That(get main "AssemblyCompanyAttribute", Is.EqualTo (Some "Co"))
        Assert.That(get attrs "AssemblyCompanyAttribute", Is.EqualTo (Some "Other"))
        Assert.That(get attrs "AssemblyTrademarkAttribute", Is.EqualTo (Some "say \"hi\"\nnext line: 日本 ü"))
        Assert.That(get attrs "AssemblyProductAttribute", Is.EqualTo (Some "Lib \"ü\""))
        // AssemblyCulture is a pseudo-attribute: the assembly's culture, not a custom attribute
        Assert.That(Reflection.AssemblyName.GetAssemblyName(Path.GetFullPath "attrs/net8.0/ja/lib.resources.dll").CultureName, Is.EqualTo "ja")

    [<Test; Category("Integration")>]
    member x.``a wrongly cased culture folder fails``() =
        x.Fixture ()
        Directory.CreateDirectory "translations/zh-cn" |> ignore
        File.WriteAllText ("translations/zh-cn/Strings.resx", resx [ "Hello", "Ni hao" ])
        let text = x.Fails "zh-cn" [ "case/net8.0/zh-cn/lib.resources.dll" ] (fun _ fwk culture -> x.Usual fwk culture)
        Assert.That(text, Does.Contain "the culture folder 'zh-cn' is spelled 'zh-CN' by .NET")

    [<Test; Category("Integration")>]
    member x.``a file of another culture fails``() =
        x.Fixture ()
        Directory.CreateDirectory "translations/ja-other" |> ignore
        File.WriteAllText ("translations/ja-other/Strings.zh-CN.resx", resx [ "Hello", "Ni hao" ])
        let text =
            x.Fails "other-culture" [ "other/net8.0/ja/lib.resources.dll" ] (fun _ fwk _ ->
                satellite { template (x.Template fwk); keyfile "key.snk"; resources (resourceset { prefix "lib"; basedir "translations/ja-other"; includes "*.resx" }) })
        Assert.That(text, Does.Contain "is a resource of the culture 'zh-CN', not of the satellite's 'ja'")

    [<Test; Category("Integration")>]
    member x.``the key must give the template's token``() =
        x.Fixture ()
        let set = resourceset { prefix "lib"; basedir "translations/ja"; includes "*.resx" }
        let nokey = x.Fails "nokey" [ "nokey/net8.0/ja/lib.resources.dll" ] (fun _ fwk _ -> satellite { template (x.Template fwk); resources set })
        Assert.That(nokey, Does.Contain "'lib.dll' is signed (public key token")
        Assert.That(nokey, Does.Contain "and the satellite has no key")
        let other = x.Fails "otherkey" [ "otherkey/net8.0/ja/lib.resources.dll" ] (fun _ fwk _ -> satellite { template (x.Template fwk); keyfile "other.snk"; resources set })
        Assert.That(other, Does.Contain "the satellite's public key token is")
        Assert.That(other, Does.Contain "it would never be loaded")
        let unsigned = x.Fails "unsigned" [ "unsigned/net8.0/ja/lib.resources.dll" ] (fun _ _ _ -> satellite { template "plain/lib.dll"; keyfile "key.snk"; resources set })
        Assert.That(unsigned, Does.Contain "'lib.dll' is not signed, so its satellite must not be: drop the key")
        // without the key, the unsigned template's satellite builds
        x.Build "unsigned-ok" [ "unsigned-ok/net8.0/ja/lib.resources.dll" ] (fun _ _ _ -> satellite { template "plain/lib.dll"; resources set })
        Assert.That(Reflection.AssemblyName.GetAssemblyName(Path.GetFullPath "unsigned-ok/net8.0/ja/lib.resources.dll").GetPublicKeyToken (), Is.Empty)

    [<Test; Category("Integration")>]
    member x.``no resources, a name twice, a different targetfwk, a misnamed output fail``() =
        x.Fixture ()
        Directory.CreateDirectory "translations/empty" |> ignore
        Directory.CreateDirectory "translations/twice/ja" |> ignore
        File.WriteAllText ("translations/twice/ja/Strings.resx", resx [ "Hello", "Mata" ])
        let empty = x.Fails "empty" [ "empty/net8.0/ja/lib.resources.dll" ] (fun _ fwk _ ->
            satellite { template (x.Template fwk); keyfile "key.snk"; resources (resourceset { prefix "lib"; basedir "translations/empty"; includes "*.resx" }) })
        Assert.That(empty, Does.Contain "no resources for the satellite of 'ja'")
        let twice = x.Fails "twice" [ "twice/net8.0/ja/lib.resources.dll" ] (fun _ fwk _ ->
            satellite {
                template (x.Template fwk); keyfile "key.snk"
                resources (resourceset { prefix "lib"; basedir "translations/ja"; includes "*.resx" })
                resources (resourceset { prefix "lib"; basedir "translations/twice/ja"; includes "*.resx" }) })
        Assert.That(twice, Does.Contain "the satellite would embed lib.Strings.ja.resources twice")
        let fwk = x.Fails "fwk" [ "fwk/net8.0/ja/lib.resources.dll" ] (fun _ fwk _ ->
            satellite { template (x.Template fwk); targetfwk "net472"; keyfile "key.snk"; resources (resourceset { prefix "lib"; basedir "translations/ja"; includes "*.resx" }) })
        Assert.That(fwk, Does.Contain "targetfwk 'net472' differs from the framework of 'lib.dll' (net8.0)")
        let named = x.Fails "named" [ "named/net8.0/ja/Lib.resources.dll" ] (fun _ fwk _ ->
            satellite { template (x.Template fwk); keyfile "key.snk"; resources (resourceset { prefix "lib"; basedir "translations/ja"; includes "*.resx" }) })
        Assert.That(named, Does.Contain "the satellite of 'lib.dll' is named 'lib.resources.dll'")

    [<Test; Category("Integration")>]
    member x.``a resource without its neutral one warns, or fails with NeutralCheck.Fail``() =
        x.Fixture ()
        Directory.CreateDirectory "translations/orphan/ja" |> ignore
        File.WriteAllText ("translations/orphan/ja/Strings.resx", resx [ "Hello", "Konnichiwa" ])
        File.WriteAllText ("translations/orphan/ja/Orphan.resx", resx [ "A", "B" ])
        let set = resourceset { prefix "lib"; basedir "translations/orphan/ja"; includes "*.resx" }
        x.Build "orphan-warn" [ "orphan/net8.0/ja/lib.resources.dll" ] (fun _ fwk _ -> satellite { template (x.Template fwk); keyfile "key.snk"; resources set })
        Assert.That(File.ReadAllText "orphan-warn.log", Does.Contain "lib.Orphan.ja.resources has no neutral resource of that name in 'lib.dll'")
        let text = x.Fails "orphan-fail" [ "orphan-fail/net8.0/ja/lib.resources.dll" ] (fun _ fwk _ ->
            satellite { template (x.Template fwk); keyfile "key.snk"; resources set; neutralcheck NeutralCheck.Fail })
        Assert.That(text, Does.Contain "lib.Orphan.ja.resources has no neutral resource of that name in 'lib.dll'")

    [<Test; Category("Integration")>]
    member x.``an added translation rebuilds the satellite``() =
        x.Fixture ()
        if Directory.Exists "translations/grow" then Directory.Delete ("translations/grow", true)
        Directory.CreateDirectory "translations/grow/ja" |> ignore
        File.WriteAllText ("translations/grow/ja/Strings.resx", resx [ "Hello", "Konnichiwa" ])
        let build label =
            x.Build label [ "grow/net8.0/ja/lib.resources.dll" ] (fun _ fwk _ ->
                satellite {
                    template (x.Template fwk); keyfile "key.snk"
                    resources (resourceset { prefix "lib"; basedir "translations/grow/ja"; includes "**/*.resx" }) })
        let resources () = (Satellite.readTemplate "grow/net8.0/ja/lib.resources.dll").Resources
        build "grow-1"
        Assert.That(resources (), Is.EqualTo [ "lib.Strings.ja.resources" ])
        Directory.CreateDirectory "translations/grow/ja/Forms" |> ignore
        File.WriteAllText ("translations/grow/ja/Forms/Main.resx", resx [ "Title", "Shu" ])
        build "grow-2"
        Assert.That(resources (), Is.EquivalentTo [ "lib.Strings.ja.resources"; "lib.Forms.Main.ja.resources" ])
