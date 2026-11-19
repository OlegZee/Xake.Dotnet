// The Windows acceptance of the OS-independent source order (docs/features/source-order/windows/README.md).
// `pkg/` (a source in nuget.config) holds the Xake.Dotnet package under test (`dotnet fsi build.fsx -- -- pack -d VERSION=<v>`).
//   dotnet fsi accept.fsx -- -- record     records locks/globs/Release.json
//   dotnet fsi accept.fsx -- -- build      builds it
#r "nuget: Xake.Dotnet, 4.4.0-srcorder.1"

open Xake
open Xake.Hermetic.Dotnet

let lockOf name cfg = sprintf "locks/%s/%s.json" name cfg
let outOf cfg fwk name = sprintf "out/%s/%s/%s" cfg fwk name

do xakeScript {
    rules [
        command "record" {
            let! _ = Import.record { Import.RecordOptions.Default with LockOf = lockOf } [ "globs/globs.csproj" ]
            return () }
        command "build" { do! Locks.needAll (!! "*/xake.project.json") "Release" lockOf outOf }
        "out/(cfg:*)/(fwk:*)/(name:*)/*.dll" ..> recipe {
            let! cfg = getRuleMatch "cfg"
            let! fwk = getRuleMatch "fwk"
            let! name = getRuleMatch "name"
            do! Locks.compile Locks.Settings.Default (lockOf name cfg) fwk }
    ]
}
