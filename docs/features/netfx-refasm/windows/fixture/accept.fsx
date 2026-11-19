// The Windows acceptance of NetFxReferenceAssemblies (docs/features/netfx-refasm/windows/README.md).
// `pkg/` (a source in nuget.config) holds the Xake.Dotnet package under test (`dotnet fsi build.fsx -- -- pack -d VERSION=<v>`).
//   dotnet fsi accept.fsx -- -- package     records locks/ with NetFxReferenceAssemblies.Package (the default)
//   dotnet fsi accept.fsx -- -- installed   records locks-installed/ with NetFxReferenceAssemblies.Installed
//   dotnet fsi accept.fsx -- -- build       builds the Package locks
#r "nuget: Xake.Dotnet, 4.4.0-netfx.1"

open Xake
open Xake.Hermetic.Dotnet

let lockOf name cfg = sprintf "locks/%s/%s.json" name cfg
let outOf cfg fwk name = sprintf "out/%s/%s/%s" cfg fwk name
let projects = [ "fx/fx.csproj"; "cpm/cpm.csproj" ]
let record (locks: string) mode =
    recipe {
        let options = { Import.RecordOptions.Default with NetFxReferenceAssemblies = mode; LockOf = (fun name cfg -> sprintf "%s/%s/%s.json" locks name cfg) }
        let! _ = Import.record options projects
        return ()
    }

do xakeScript {
    rules [
        command "package" { do! record "locks" Import.NetFxReferenceAssemblies.Package }
        command "installed" { do! record "locks-installed" Import.NetFxReferenceAssemblies.Installed }
        command "build" { do! Locks.needAll (!! "*/xake.project.json") "Release" lockOf outOf }
        "out/(cfg:*)/(fwk:*)/(name:*)/*.dll" ..> recipe {
            let! cfg = getRuleMatch "cfg"
            let! fwk = getRuleMatch "fwk"
            let! name = getRuleMatch "name"
            do! Locks.compile Locks.Settings.Default (lockOf name cfg) fwk }
    ]
}
