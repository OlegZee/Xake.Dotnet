namespace Xake.Hermetic.Dotnet

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text

open Xake.Dotnet

/// <summary>
/// The repository's own spelling of a file: what the import records in place of the project
/// file's (`Foo\Bar.CS` for `foo/Bar.cs`), so that a description or a lock recorded on a file
/// system that ignores case (macOS, Windows) builds on one that does not (Linux). The source of
/// truth is the git index, read with `git ls-files` once per import (never parsed here); a path
/// that is not in the index, or outside the folder it was read for, stays as it is. Only the
/// import resolves paths: a build or a replay reads exactly the paths a lock names.
/// Page: `docs/hermetic/description.md` "Import".
/// </summary>
module Casing =

    /// The files git tracks under one folder (`Root`, absolute, `/`), by their path relative to
    /// it in lower case: every spelling the index has (two differ only by case on a file system
    /// that keeps both); the same for the folders above them.
    type Index =
        internal
            { Root: string
              Files: Dictionary<string, string list>
              Folders: Dictionary<string, string list> }

    let private slash (p: string) = p.Replace ('\\', '/')

    /// `git <arguments>` run in `dir`: its standard output (UTF-8), or why it gave none (git is
    /// not installed, `dir` is not in a repository).
    let private git (dir: string) (arguments: string) : Result<string, string> =
        let psi = ProcessStartInfo ("git", arguments)
        psi.WorkingDirectory <- dir
        psi.UseShellExecute <- false
        psi.RedirectStandardOutput <- true
        psi.RedirectStandardError <- true
        psi.StandardOutputEncoding <- UTF8Encoding false
        psi.StandardErrorEncoding <- UTF8Encoding false
        try
            use p = Process.Start psi
            let err = p.StandardError.ReadToEndAsync ()
            let out = p.StandardOutput.ReadToEnd ()
            p.WaitForExit ()
            if p.ExitCode = 0 then Ok out
            else Error (sprintf "'git %s' in '%s' failed: %s" arguments dir (err.Result.Trim ()))
        with e -> Error (sprintf "git could not be run (%s)" e.Message)

    /// The index from the paths `git ls-files` lists under `root`, relative to it. Pure.
    let internal ofFiles (root: string) (files: string seq) : Index =
        let add (map: Dictionary<string, string list>) (key: string) (spelling: string) =
            match map.TryGetValue key with
            | true, spellings when List.contains spelling spellings -> ()
            | true, spellings -> map.[key] <- spellings @ [ spelling ]
            | _ -> map.[key] <- [ spelling ]
        let index = { Root = (slash root).TrimEnd '/'; Files = Dictionary (); Folders = Dictionary () }
        for f in files do
            add index.Files (f.ToLowerInvariant ()) f
            let parts = f.Split '/'
            for i in 1 .. parts.Length - 1 do
                let folder = String.Join ("/", parts, 0, i)
                add index.Folders (folder.ToLowerInvariant ()) folder
        index

    /// <summary>
    /// The index of the repository files under `root` (`git ls-files -z --full-name`, made
    /// relative to `root` by `git rev-parse --show-prefix`), or why there is none: git is not
    /// installed or `root` is in no repository. Read once per import.
    /// </summary>
    let read (root: string) : Result<Index, string> =
        match git root "rev-parse --show-prefix" with
        | Error e -> Error e
        | Ok prefix ->
            let prefix = slash (prefix.Trim ())
            match git root "ls-files -z --full-name" with
            | Error e -> Error e
            | Ok listed ->
                let files =
                    listed.Split ([| '\000' |], StringSplitOptions.RemoveEmptyEntries)
                    |> Seq.choose (fun f ->
                        if f.StartsWith (prefix, StringComparison.Ordinal) then Some (f.Substring prefix.Length)
                        elif f.StartsWith (prefix, StringComparison.OrdinalIgnoreCase) then Some (f.Substring prefix.Length)
                        else None)
                Ok (ofFiles root files)

    /// The one spelling of `rel` among `spellings`: the exact one when the index has it, the only
    /// one, else a failure naming them all.
    let private pick (index: Index) (rel: string) (spellings: string list) =
        match spellings with
        | [ s ] -> s
        | _ when List.contains rel spellings -> rel
        | _ ->
            failwithf "'%s/%s' is one of %s in the git index, which differ only by case; rename all but one of them (git mv) and import again"
                index.Root rel (spellings |> List.map (sprintf "'%s'") |> String.concat ", ")

    /// <summary>
    /// `path` (absolute; `\` read as `/`) in the repository's spelling: under the index's folder,
    /// the file the index has whose path differs from it only by case -- or, when it names no
    /// file, such a folder -- with `/`; the folder itself spelled as the index was read for it. A
    /// path outside that folder or that the index does not have is returned unchanged. Two
    /// entries of the index that differ only by case: the exact spelling wins, else this fails
    /// naming both. Pure.
    /// </summary>
    let resolve (index: Index) (path: string) : string =
        let p = slash path
        let p = if p.Contains "/../" || p.Contains "/./" then slash (Path.GetFullPath p) else p
        let trailing = p.Length > 1 && p.EndsWith "/"
        let p' = if trailing then p.TrimEnd '/' else p
        if p'.Length <= index.Root.Length || p'.[index.Root.Length] <> '/'
           || not (p'.StartsWith (index.Root, StringComparison.OrdinalIgnoreCase)) then path
        else
            let rel = p'.Substring (index.Root.Length + 1)
            let key = rel.ToLowerInvariant ()
            let found =
                match index.Files.TryGetValue key with
                | true, spellings -> Some (pick index rel spellings)
                | _ ->
                    match index.Folders.TryGetValue key with
                    | true, spellings -> Some (pick index rel spellings)
                    | _ -> None
            match found with
            | Some spelling ->
                let result = index.Root + "/" + spelling + (if trailing then "/" else "")
                if result = slash path then path else result
            | None -> path

    /// <summary>
    /// The repositories of one import: for each path, the git index of the repository that
    /// contains it -- found from the nearest existing folder of the path by
    /// `git rev-parse --show-cdup`, cached per folder; a submodule is a repository of its own --
    /// and each repository's index read once (`read`). Paths under `excluded` (the package
    /// folder, the .NET installation: never repository files) are not looked up.
    /// </summary>
    type Repos (excluded: string list) =
        let excluded = excluded |> List.filter ((<>) "") |> List.map (fun e -> (slash e).TrimEnd '/' + "/")
        let topOfDir = Collections.Concurrent.ConcurrentDictionary<string, Lazy<Result<string, string>>> ()
        let indexOfTop = Collections.Concurrent.ConcurrentDictionary<string, Lazy<Result<Index, string>>> ()
        let canonicalOfTop = Collections.Concurrent.ConcurrentDictionary<string, Lazy<string>> ()
        let outside = Collections.Concurrent.ConcurrentDictionary<string, string> ()

        /// The top of the repository the folder `dir` (existing, '/') is in, spelled as `dir` is.
        let topOf (dir: string) =
            topOfDir.GetOrAdd(dir, fun d ->
                lazy (
                    match git d "rev-parse --show-cdup" with
                    | Error e -> Error e
                    | Ok cdup -> Ok ((slash (Path.GetFullPath (Path.Combine (d, cdup.Trim ())))).TrimEnd '/'))).Value

        let indexOf (top: string) =
            indexOfTop.GetOrAdd(top.ToLowerInvariant (), fun _ -> lazy (read top)).Value

        let rec existing (dir: string) =
            if dir = "" then None
            elif Directory.Exists dir then Some dir
            else match Path.GetDirectoryName dir with null -> None | parent -> existing (slash parent)

        /// A repository's top in its own spelling: a submodule's path as its superproject's index
        /// has it (the gitlink entry), recursively; a top in no repository as it is.
        let rec canonical (top: string) : string =
            canonicalOfTop.GetOrAdd(top, fun t ->
                lazy (
                    match Path.GetDirectoryName t |> Option.ofObj |> Option.map slash |> Option.bind existing with
                    | None -> t
                    | Some parent ->
                        match topOf parent with
                        | Error _ -> t
                        | Ok outer when t.Length > outer.Length && t.[outer.Length] = '/' ->
                            let outer' = canonical outer
                            match indexOf outer' with
                            | Ok index -> resolve index (outer' + t.Substring outer.Length)
                            | Error _ -> outer' + t.Substring outer.Length
                        | Ok _ -> t)).Value

        /// `path` (absolute; `\` read as `/`) in the spelling of the repository that contains it
        /// (`resolve` against that repository's index, its top in its own spelling); a path
        /// excluded, in no repository or that its index does not have unchanged.
        member _.Resolve (path: string) : string =
            let p = slash path
            let p = if p.Contains "/../" || p.Contains "/./" then slash (Path.GetFullPath p) else p
            let trailing = p.Length > 1 && p.EndsWith "/"
            let p' = if trailing then p.TrimEnd '/' else p
            if excluded |> List.exists (fun e -> (p' + "/").StartsWith (e, StringComparison.OrdinalIgnoreCase)) then path else
            match existing p' |> Option.orElse (Path.GetDirectoryName p' |> Option.ofObj |> Option.bind (slash >> existing)) with
            | None -> path
            | Some dir ->
                match topOf dir with
                | Error reason ->
                    outside.TryAdd ((if reason.StartsWith "git could not be run" then "" else dir), reason) |> ignore
                    path
                | Ok top ->
                    let top' = canonical top
                    let result =
                        if p' = top then top'
                        else
                            match indexOf top' with
                            | Error _ -> p'
                            | Ok index ->
                                match resolve index (top' + p'.Substring top.Length) with
                                | r when r = top' + p'.Substring top.Length && top' <> top -> p'
                                | r -> slash r
                    let result = result + (if trailing then "/" else "")
                    if result = p then path else result

        /// Whether `path` (absolute) is a file of the repository that contains it (in its index,
        /// in any spelling): a path excluded, in no repository or untracked is not.
        member this.IsTracked (path: string) : bool =
            let p = slash path
            if excluded |> List.exists (fun e -> p.StartsWith (e, StringComparison.OrdinalIgnoreCase)) then false else
            match existing (match Path.GetDirectoryName p with null -> "" | d -> slash d) with
            | None -> false
            | Some dir ->
                match topOf dir with
                | Error _ -> false
                | Ok top ->
                    match indexOf (canonical top) with
                    | Error _ -> false
                    | Ok index ->
                        let resolved = slash (this.Resolve p)
                        let top' = canonical top
                        resolved.Length > top'.Length + 1 && resolved.StartsWith (top' + "/", StringComparison.OrdinalIgnoreCase)
                        && index.Files.ContainsKey (resolved.Substring(top'.Length + 1).ToLowerInvariant ())

        /// The folders (topmost, sorted) whose files are in no repository, with why: "" for no git.
        member _.Outside : (string * string) list =
            let dirs = outside |> Seq.map (fun kv -> kv.Key, kv.Value) |> Seq.sortBy fst |> List.ofSeq
            dirs |> List.filter (fun (d, _) -> not (dirs |> List.exists (fun (o, _) -> o <> d && o <> "" && d.StartsWith (o + "/"))))

    /// <summary>
    /// What one project of an import resolves with: the import's repositories (`None`: every path
    /// stays as it is) and every difference it found between a path as the project spelled it and
    /// the repository's spelling, (project's, repository's), once each.
    /// </summary>
    type Resolver (repos: Repos option) =
        let found = Collections.Concurrent.ConcurrentDictionary<string, string> ()

        /// A resolver that changes nothing.
        static member Unchanged = Resolver (Option<Repos>.None)

        /// `path` (absolute) in the repository's spelling (`Repos.Resolve`), the difference noted.
        member _.Path (path: string) : string =
            match repos with
            | None -> path
            | Some r ->
                let resolved = r.Resolve path
                if slash resolved <> slash path then
                    let p = slash path
                    found.TryAdd ((if p.Contains "/../" || p.Contains "/./" then slash (Path.GetFullPath p) else p), resolved) |> ignore
                resolved

        /// Whether `path` (absolute) is a file of the repository that contains it (`Repos.IsTracked`).
        member _.IsTracked (path: string) : bool =
            match repos with
            | None -> false
            | Some r -> r.IsTracked path

        /// `path` (absolute) in the repository's spelling, the difference not noted: what the
        /// caller reports in a line of its own (a reference the import does not rewrite).
        member _.Peek (path: string) : string =
            match repos with
            | None -> path
            | Some r -> r.Resolve path

        /// `path` relative to the folder `dir` (absolute), or absolute, in the repository's
        /// spelling (`Path`); in the form it was given -- relative to `dir` with `/` when it
        /// was -- and unchanged when the repository spells it so.
        member this.Relative (dir: string) (path: string) : string =
            let p = slash path
            if Path.IsPathRooted p then this.Path p
            else
                let full = slash (Path.GetFullPath (Path.Combine (dir, p)))
                let r = this.Path full
                if r = full then path
                else
                    // the same number of folders up, then the repository's spelling of the rest
                    let up = p.Split '/' |> Array.takeWhile (fun s -> s = ".." || s = ".") |> Array.filter ((=) "..") |> Array.length
                    let parts = (slash dir).TrimEnd('/').Split '/'
                    let baseDir = String.Join ("/", parts, 0, parts.Length - up)
                    if r.StartsWith (baseDir + "/", StringComparison.OrdinalIgnoreCase) then
                        String.replicate up "../" + r.Substring (baseDir.Length + 1)
                    else r

        /// The differences found so far, (project's spelling, repository's), sorted.
        member _.Differences : (string * string) list =
            found |> Seq.map (fun kv -> kv.Key, kv.Value) |> Seq.sortWith (fun (a, _) (b, _) -> String.CompareOrdinal (a, b)) |> List.ofSeq

    /// The line of an import's log for a file a project spells otherwise than the repository
    /// (`Resolver.Differences`): both spellings, relative to `root` when under it.
    let line (root: string) (spelled: string, repository: string) =
        let under = (slash root).TrimEnd '/' + "/"
        let rel (p: string) = if p.StartsWith (under, StringComparison.OrdinalIgnoreCase) then p.Substring under.Length else p
        sprintf "'%s' is '%s' in the repository (git index): recorded so" (rel spelled) (rel repository)

    /// <summary>
    /// An argument of a compiler's command line (paths absolute) with every path it names in the
    /// repository's spelling (`casing`). The resource a `/resource:` or `/linkresource:` without
    /// a name embeds is named by the compiler after its file name, as the project spells it:
    /// where the repository spells the file name otherwise, that name follows the path
    /// (`path,name`), so the resource keeps the name msbuild gives it.
    /// </summary>
    let argument (isFSharp: bool) (casing: Resolver) (arg: string) =
        let named (canonical: string) (value: string) =
            match canonical, CscArgs.splitList value with
            | ("resource" | "linkresource"), [ path ] ->
                let r = casing.Path path
                if Path.GetFileName r = Path.GetFileName path then None
                else Some (CscArgs.quoteIfNeeded r + "," + Path.GetFileName path)
            | _ -> None
        if arg.StartsWith "@" then arg
        elif isFSharp then
            let a = FscArgs.parse arg
            match a with
            | FscArgs.Switch (prefix, name, value) ->
                match named (FscArgs.canonical name) value with
                | Some v -> FscArgs.format (FscArgs.Switch (prefix, name, v))
                | None -> FscArgs.mapPaths casing.Path a |> FscArgs.format
            | _ -> FscArgs.mapPaths casing.Path a |> FscArgs.format
        else
            let a = CscArgs.parse arg
            match a with
            | CscArgs.Switch (name, value) ->
                match named (CscArgs.canonical name) value with
                | Some v -> CscArgs.format (CscArgs.Switch (name, v))
                | None -> CscArgs.mapPaths casing.Path a |> CscArgs.format
            | _ -> CscArgs.mapPaths casing.Path a |> CscArgs.format
