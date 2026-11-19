namespace Tests

open System.IO
open NUnit.Framework

open Xake.Hermetic.Dotnet

/// `Import.record` of .NET Framework projects with `RecordOptions.NetFxReferenceAssemblies`
/// (docs/hermetic/lock.md, ".NET Framework reference assemblies"): the reference assemblies come
/// from `Microsoft.NETFramework.ReferenceAssemblies` (here, where no targeting pack is installed,
/// and on Windows where one is: the acceptance in docs/features/netfx-refasm/windows/), under
/// central package management too; other frameworks record the same lock either way.
[<TestFixture>]
type ``Import record netfx reference assemblies``() =
    inherit XakeTestBase("import-record-netfx")

    let refasm (fwk: string) (version: string) = sprintf "pkg:microsoft.netframework.referenceassemblies.%s/%s/build/.NETFramework/" fwk version

    let fixture () =
        RecordKit.write [ "fx"; "cpm"; "core"; "std"; "out"; "locks"; "obj" ]
            [ // System.Management is in the package; System.Management.Automation is in a GAC only
              "fx/fx.csproj",
                RecordKit.sdkProject "<TargetFrameworks>net472;netstandard2.0</TargetFrameworks>"
                    "<Reference Include=\"System.Management\" Condition=\"'$(TargetFramework)' == 'net472'\" /><Reference Include=\"System.Management.Automation\" Condition=\"'$(TargetFramework)' == 'net472'\" />"
              "fx/C.cs", "namespace Fx { public class C {\n#if NET472\n  public static string S() => typeof (System.Management.ManagementObject).Name;\n#endif\n} }\n"
              "cpm/Directory.Packages.props",
                "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup><ItemGroup><PackageVersion Include=\"Newtonsoft.Json\" Version=\"13.0.3\" /></ItemGroup></Project>\n"
              "cpm/cpm.csproj", RecordKit.sdkProject "<TargetFramework>net472</TargetFramework>" "<PackageReference Include=\"Newtonsoft.Json\" />"
              "cpm/C.cs", "namespace Cpm { public class C { public static string S() => Newtonsoft.Json.JsonConvert.SerializeObject (1); } }\n"
              "core/core.csproj", RecordKit.sdkProject "<TargetFrameworks>net8.0;netstandard2.0</TargetFrameworks>" ""
              "core/C.cs", "namespace Core { public class C { } }\n"
              // one framework: its restore runs with the hidden root set (the framework not known yet)
              "std/std.csproj", RecordKit.sdkProject "<TargetFramework>netstandard2.0</TargetFramework>" ""
              "std/C.cs", "namespace Std { public class C { } }\n" ]

    [<Test>]
    member x.``the package by default, under central package management too; an assembly it lacks is left out; both build``() =
        fixture ()
        RecordKit.import x.TestOptions "import" RecordKit.recordDefault [ "fx/fx.csproj"; "cpm/cpm.csproj" ] |> ignore
        let fx, cpm = File.ReadAllText (RecordKit.lockOf "fx" "Release"), File.ReadAllText (RecordKit.lockOf "cpm" "Release")
        for lock in [ fx; cpm ] do
            Assert.That(lock, Does.Contain (refasm "net472" "1.0.3" + "v4.7.2/mscorlib.dll"))
            Assert.That(lock, Does.Not.Contain "Reference Assemblies")
        Assert.That(fx, Does.Contain (refasm "net472" "1.0.3" + "v4.7.2/System.Management.dll"))
        Assert.That(fx, Does.Not.Contain "System.Management.Automation")
        Assert.That(cpm, Does.Contain "pkg:newtonsoft.json/13.0.3/")
        RecordKit.build x.TestOptions "build" "Release"
        Assert.That(File.Exists "out/Release/net472/fx/fx.dll")
        Assert.That(File.Exists "out/Release/net472/cpm/cpm.dll")

    [<Test>]
    member x.``the configured version is the package's in the lock``() =
        fixture ()
        RecordKit.import x.TestOptions "import-1.0.0" { RecordKit.recordDefault with NetFxReferenceAssembliesVersion = "1.0.0" } [ "cpm/cpm.csproj" ] |> ignore
        let cpm = File.ReadAllText (RecordKit.lockOf "cpm" "Release")
        Assert.That(cpm, Does.Contain (refasm "net472" "1.0.0"))
        Assert.That(cpm, Does.Not.Contain "/1.0.3/")

    [<Test>]
    member x.``projects of other frameworks record the same locks with Package and Installed``() =
        fixture ()
        let record label mode =
            RecordKit.import x.TestOptions label { RecordKit.recordDefault with NetFxReferenceAssemblies = mode } [ "core/core.csproj"; "std/std.csproj" ] |> ignore
            File.ReadAllBytes (RecordKit.lockOf "core" "Release"), File.ReadAllBytes (RecordKit.lockOf "std" "Release")
        let installed = record "import-installed" Import.NetFxReferenceAssemblies.Installed
        let package = record "import-package" Import.NetFxReferenceAssemblies.Package
        Assert.That(package, Is.EqualTo installed)
