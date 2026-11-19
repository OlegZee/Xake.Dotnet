namespace Tests

open System.IO
open System.Text.RegularExpressions
open NUnit.Framework

open Xake
open Xake.Hermetic.Dotnet

/// `Import.record` of a legacy (non-SDK) project with the import overlay of
/// docs/legacy-projects.md, taken from that page's xml block as is, and the guard that fails such
/// a project without it. The Windows run (targeting packs installed): docs/features/legacy-recipe/windows/.
[<TestFixture>]
type ``Import record legacy projects``() =
    inherit XakeTestBase("import-record-legacy")

    static let docsOverlay =
        let page = File.ReadAllText (Path.Combine (__SOURCE_DIRECTORY__, "..", "..", "docs", "legacy-projects.md"))
        Regex.Match(page, "```xml\n(.*?)```", RegexOptions.Singleline).Groups.[1].Value

    // the old format as Visual Studio writes it: a library with a package and a XAML page
    let legacyProject =
        String.concat "\n"
            [ "<?xml version=\"1.0\" encoding=\"utf-8\"?>"
              "<Project ToolsVersion=\"15.0\" DefaultTargets=\"Build\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">"
              "  <Import Project=\"$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props\" Condition=\"Exists('$(MSBuildExtensionsPath)\\$(MSBuildToolsVersion)\\Microsoft.Common.props')\" />"
              "  <PropertyGroup>"
              "    <Configuration Condition=\" '$(Configuration)' == '' \">Debug</Configuration>"
              "    <OutputType>Library</OutputType>"
              "    <AssemblyName>Legacy</AssemblyName>"
              "    <TargetFrameworkVersion>v4.7.2</TargetFrameworkVersion>"
              "    <OutputPath>bin\\$(Configuration)\\</OutputPath>"
              "  </PropertyGroup>"
              "  <ItemGroup>"
              "    <Reference Include=\"System\" /><Reference Include=\"System.Xaml\" /><Reference Include=\"WindowsBase\" />"
              "    <Reference Include=\"PresentationCore\" /><Reference Include=\"PresentationFramework\" />"
              "    <Compile Include=\"C.cs\" />"
              "    <Compile Include=\"W.xaml.cs\"><DependentUpon>W.xaml</DependentUpon></Compile>"
              "    <Page Include=\"W.xaml\"><Generator>MSBuild:Compile</Generator><SubType>Designer</SubType></Page>"
              "    <PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />"
              "  </ItemGroup>"
              "  <Import Project=\"$(MSBuildToolsPath)\\Microsoft.CSharp.targets\" />"
              "</Project>"
              "" ]

    let fixture (overlay: string) =
        RecordKit.write [ "legacy"; "sdk"; "build"; "out"; "locks"; "obj" ]
            [ "build/legacy.targets", overlay
              "legacy/legacy.csproj", legacyProject
              "legacy/C.cs", "namespace Legacy { public class C { public static string S() => Newtonsoft.Json.JsonConvert.SerializeObject (1); } }\n"
              "legacy/W.xaml",
                "<UserControl x:Class=\"Legacy.W\" xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">\n  <TextBlock Text=\"legacy\" />\n</UserControl>\n"
              "legacy/W.xaml.cs", "namespace Legacy { public partial class W : System.Windows.Controls.UserControl { public W() { InitializeComponent (); } } }\n"
              "sdk/sdk.csproj", RecordKit.sdkProject "<TargetFramework>net8.0</TargetFramework>" "<PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />"
              "sdk/C.cs", "namespace Sdk { public class C { public static string S() => Newtonsoft.Json.JsonConvert.SerializeObject (1); } }\n" ]

    let withOverlay () = { RecordKit.recordDefault with Properties = [ "CustomAfterMicrosoftCommonTargets", Path.GetFullPath "build/legacy.targets" ] }

    let failure (options: ExecOptions) (record: Import.RecordOptions) =
        let e = Assert.Throws<XakeException>(fun () -> RecordKit.import options "import" record [ "legacy/legacy.csproj" ] |> ignore)
        string e

    [<Test>]
    member x.``the docs' overlay records a legacy project with a package and XAML; it builds``() =
        fixture docsOverlay
        RecordKit.import x.TestOptions "import" (withOverlay ()) [ "legacy/legacy.csproj" ] |> ignore
        let lock = File.ReadAllText (RecordKit.lockOf "legacy" "Release")
        Assert.That(lock, Does.Contain "pkg:microsoft.netframework.referenceassemblies.net472/1.0.3/build/.NETFramework/v4.7.2/mscorlib.dll")
        Assert.That(lock, Does.Contain "pkg:newtonsoft.json/13.0.3/lib/net45/Newtonsoft.Json.dll")
        Assert.That(lock, Does.Contain "/resource:$(ProjectRoot)/locks/legacy/Release.files/net472/Legacy.g.resources")
        // the overlay is an import input: its edit makes the lock stale
        Assert.That(lock, Does.Contain "{ \"Path\": \"$(ProjectRoot)/build/legacy.targets\"")
        Assert.That(lock, Does.Not.Contain "Reference Assemblies")
        RecordKit.build x.TestOptions "build" "Release"
        Assert.That(File.Exists "out/Release/net472/legacy/Legacy.dll")

    [<Test>]
    member x.``an SDK project records the same lock with the overlay, but for the overlay as an input``() =
        fixture docsOverlay
        let record label options =
            RecordKit.import x.TestOptions label options [ "sdk/sdk.csproj" ] |> ignore
            File.ReadAllText (RecordKit.lockOf "sdk" "Release")
        let plain = record "import-plain" RecordKit.recordDefault
        let overlaid = record "import-overlay" (withOverlay ())
        let dropOverlay (lock: string) =
            Regex.Replace(lock, "\\s*\"CustomAfterMicrosoftCommonTargets\": \"[^\"]*\"|,\\s*\\{ \"Path\": \"\\$\\(ProjectRoot\\)/build/legacy.targets\"[^}]*\\}", "")
            |> fun s -> Regex.Replace(s, "\\{\\s*\\}", "{}")
        Assert.That(dropOverlay overlaid, Is.EqualTo (dropOverlay plain))
        Assert.That(overlaid, Is.Not.EqualTo plain)

    [<Test>]
    member x.``without the overlay the import fails naming every missing section``() =
        fixture docsOverlay
        let message = failure x.TestOptions RecordKit.recordDefault
        Assert.That(message, Does.Contain "legacy.csproj': legacy (non-SDK) project; import needs TargetFramework and PackageReference resolution — see docs/legacy-projects.md (missing: A framework, B package resolution, D reference assemblies)")

    [<Test>]
    member x.``with section A alone the import fails naming B and D``() =
        let sectionA = Regex.Match(docsOverlay, "  <!-- A\\..*?</PropertyGroup>\n", RegexOptions.Singleline).Value
        Assert.That(sectionA, Does.Contain "<TargetFramework>")
        fixture ("<Project>\n" + sectionA + "</Project>\n")
        let message = failure x.TestOptions (withOverlay ())
        Assert.That(message, Does.Contain "(missing: B package resolution, D reference assemblies)")

    [<Test>]
    member x.``the guard reads the evaluation: an SDK project never trips it``() =
        let dump props packages : Import.Dump =
            { Props = Map.ofList props; Items = Map.ofList [ "PackageReference", [ for p in packages -> p, Map.empty ] ] }
        let complete =
            [ "TargetFramework", "net472"; "NETCoreSdkVersion", "11.0.100"; "ProjectAssetsCacheFile", "obj/x.assets.cache"
              "TargetFrameworkIdentifier", ".NETFramework"; "AutomaticallyUseReferenceAssemblyPackages", "true" ]
        Assert.That(Import.legacyGaps (dump [ "UsingMicrosoftNETSdk", "true" ] []), Is.Empty)
        Assert.That(Import.legacyGaps (dump complete [ "Microsoft.NETFramework.ReferenceAssemblies" ]), Is.Empty)
        Assert.That(Import.legacyGaps (dump complete []), Is.EqualTo [ "D reference assemblies" ])
        // the installed packs (NetFxReferenceAssemblies.Installed): no package wanted
        Assert.That(Import.legacyGaps (dump (complete |> List.filter (fst >> (<>) "AutomaticallyUseReferenceAssemblyPackages")) []), Is.Empty)
        Assert.That(Import.legacyGaps (dump [ "TargetFrameworkIdentifier", ".NETFramework" ] []), Is.EqualTo [ "A framework"; "B package resolution" ])
