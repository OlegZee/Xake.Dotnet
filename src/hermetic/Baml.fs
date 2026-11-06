namespace Xake.Hermetic.Dotnet

open System
open System.IO

/// <summary>
/// The assemblies a WPF project's compiled XAML (BAML) names. The markup compiler writes every
/// page as a `.baml` stream of `&lt;assembly&gt;.g.resources`; each assembly whose types a page
/// uses is an `AssemblyInfo` record with its full name, version included, as the compiler loaded
/// it. A `g.resources` recorded beside a lock therefore pins the versions the import saw
/// (`Lock.Recorded.Markup`).
/// </summary>
module Baml =

    /// A 7-bit encoded int (`BinaryWriter.Write7BitEncodedInt`) at `i`: (value, its bytes).
    let private int7 (b: byte[]) (i: int) =
        let rec go value shift n =
            if n > 4 || i + n >= b.Length then None else
            let x = int b.[i + n]
            let value = value ||| ((x &&& 0x7f) <<< shift)
            if x &&& 0x80 = 0 then Some (value, n + 1) else go value (shift + 7) (n + 1)
        go 0 0 0

    /// <summary>
    /// The full names of the `AssemblyInfo` records of one BAML stream, distinct, in order. The
    /// record: the type byte 0x1C, its size (7-bit encoded, counting its own bytes), an int16 id,
    /// the name as a length-prefixed UTF-8 string. A scan rather than a walk over every record
    /// type: a candidate counts only when its size is exactly what its name takes and the name
    /// parses as an assembly name with a version.
    /// </summary>
    let assemblyNames (baml: byte[]) : string list =
        let utf8 = Text.UTF8Encoding (false, true)
        let names = ResizeArray<string> ()
        for i in 0 .. baml.Length - 1 do
            if baml.[i] = 0x1Cuy then
                match int7 baml (i + 1) with
                | Some (size, n) ->
                    let at = i + 1 + n + 2
                    match int7 baml at with
                    | Some (length, m) when length > 0 && size = n + 2 + m + length && at + m + length <= baml.Length ->
                        let name = try Some (utf8.GetString (baml, at + m, length)) with _ -> None
                        let parsed = name |> Option.bind (fun s -> try Some (Reflection.AssemblyName s) with _ -> None)
                        match name, parsed with
                        | Some s, Some a when s.Contains "Version=" && not (isNull a.Version) -> names.Add s
                        | _ -> ()
                    | _ -> ()
                | None -> ()
        names |> Seq.distinct |> List.ofSeq

    /// The full names the BAML streams (`*.baml`) of a `g.resources` file name, distinct, in order.
    let ofResources (path: string) : string list =
        use reader = new Resources.ResourceReader (path)
        let keys = ResizeArray<string> ()
        let e = reader.GetEnumerator ()
        while e.MoveNext () do
            let key = string e.Key
            if key.EndsWith (".baml", StringComparison.OrdinalIgnoreCase) then keys.Add key
        let names = ResizeArray<string> ()
        for key in keys do
            let mutable kind = ""
            let mutable data : byte[] = null
            reader.GetResourceData (key, &kind, &data)
            // a stream: its length, then its bytes
            if kind = "ResourceTypeCode.Stream" && data.Length >= 4 then
                let length = min (BitConverter.ToInt32 (data, 0)) (data.Length - 4)
                names.AddRange (assemblyNames (Array.sub data 4 length))
        names |> Seq.distinct |> List.ofSeq

    /// Whether two full names are the same identity (name, version, culture, public key token).
    let sameIdentity (a: string) (b: string) =
        let x, y = Reflection.AssemblyName a, Reflection.AssemblyName b
        String.Equals (x.FullName, y.FullName, StringComparison.OrdinalIgnoreCase)
