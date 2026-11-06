namespace Tests

open NUnit.Framework
open Microsoft.FSharp.Reflection

open Xake
open Xake.Dotnet

/// The compiler records Xake.Hermetic.Dotnet builds (record expressions or `with` copies). Both
/// assemblies ship in one package now, so this is an in-package check: a field added here is a
/// deliberate change of both, made in one commit (the `ExecContext` row and the `withLogger` test
/// stay in Xake, which owns that record).
[<TestFixture>]
type ``Compiler record shapes``() =

    static member Records : obj[][] = [|
        [| typeof<CscTypes.CscSettingsType>; [ "Platform"; "Target"; "Out"; "Src"; "Ref"; "RefGlobal"; "Resources"; "Define"; "Unsafe"; "TargetFramework"; "CommandArgs"; "FailOnError"; "CscPath"; "Toolset"; "Server"; "Project"; "Throttle" ] |]
        [| typeof<CscTypes.RunOptions>; [ "FailOnError"; "CscPath"; "Server"; "Environment"; "Throttle" ] |]
        [| typeof<Csc>; [ "Name"; "Framework"; "Directory"; "Options"; "Defines"; "Sources"; "Generated"; "Resources"; "ResxFiles"; "RuntimeConfig"; "Dependencies" ] |]
        [| typeof<FscTypes.FscSettingsType>; [ "Platform"; "Target"; "Out"; "Src"; "Ref"; "RefGlobal"; "Resources"; "Define"; "TargetFramework"; "FscVersion"; "Doc"; "CommandArgs"; "FailOnError"; "NoFramework"; "Tailcalls"; "Project"; "Throttle" ] |]
        [| typeof<FscTypes.FscRunOptions>; [ "FailOnError"; "FscPath"; "Environment"; "Throttle" ] |]
        [| typeof<Fsc>; [ "Name"; "Framework"; "Directory"; "Options"; "Defines"; "Sources"; "Generated"; "Resources"; "ResxFiles"; "RuntimeConfig"; "Dependencies" ] |]
    |]

    [<TestCaseSource("Records")>]
    member _.``has the listed fields``(t: System.Type, fields: string list) =
        let actual = FSharpType.GetRecordFields t |> Array.map (fun p -> p.Name) |> List.ofArray
        Assert.That(actual, Is.EqualTo fields,
            sprintf "%s changed: Xake.Hermetic.Dotnet builds it; change both in one commit and update this list" t.FullName)
