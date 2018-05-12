Xake is a make utility made for .NET on F# language. Xake is inspired by [shake](https://github.com/ndmitchell/shake) build tool.

See [Xake documentation](https://github.com/xakebuild/Xake/wiki/introduction) for more details.

## Csc task

The simple script looks like:

```fsharp
#r "paket:
  nuget Xake ~> 1.0 prerelease
  nuget Xake.Dotnet ~> 1.0 prerelease //"

open Xake
open Xake.Dotnet

do xakeScript {
  rules [
    "main" <== ["helloworld.exe"]

    "helloworld.exe" ..> csc {src !!"helloworld.cs"}
  ]
}
```

This script compiles helloworld assembly from helloworld.cs file.
