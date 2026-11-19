// The Windows acceptance of the legacy-project overlay (docs/features/legacy-recipe/windows/README.md).
// `pkg/` (a source in nuget.config) holds the Xake.Dotnet package under test; `legacy-projects.md`
// beside this script is the docs page, whose xml block is the overlay, used as is.
//   dotnet fsi accept.fsx -- -- record   records locks/ with the overlay
//   dotnet fsi accept.fsx -- -- bare     records without it (expected to fail with the guard)
//   dotnet fsi accept.fsx -- -- build    builds the locks
#r "nuget: Xake.Dotnet, 4.4.0-legacy.1"

open System.IO
open System.Text.RegularExpressions
open Xake
open Xake.Hermetic.Dotnet

let lockOf name cfg = sprintf "locks/%s/%s.json" name cfg
let outOf cfg fwk name = sprintf "out/%s/%s/%s" cfg fwk name
let overlay () =
    let page = File.ReadAllText "legacy-projects.md"
    Directory.CreateDirectory "build" |> ignore
    File.WriteAllText ("build/legacy.targets", Regex.Match(page.Replace("\r\n", "\n"), "```xml\n(.*?)```", RegexOptions.Singleline).Groups.[1].Value)
    Path.GetFullPath "build/legacy.targets"
let record (properties: (string * string) list) =
    recipe {
        let! _ = Import.record { Import.RecordOptions.Default with Properties = properties } [ "legacy/legacy.csproj" ]
        return ()
    }

do xakeScript {
    rules [
        command "record" { do! record [ "CustomAfterMicrosoftCommonTargets", overlay () ] }
        command "bare" { do! record [] }
        command "build" { do! Locks.needAll (!! "*/xake.project.json") "Release" lockOf outOf }
        "out/(cfg:*)/(fwk:*)/(name:*)/*.dll" ..> recipe {
            let! cfg = getRuleMatch "cfg"
            let! fwk = getRuleMatch "fwk"
            let! name = getRuleMatch "name"
            do! Locks.compile Locks.Settings.Default (lockOf name cfg) fwk }
    ]
}
