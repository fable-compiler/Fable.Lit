#load "node_modules/fable-publish-utils/PublishUtils.fs"

open System
open PublishUtils

// ATTENTION: Packages must appear in dependency order
let packages = [
    "ConstructStyleSheetsPolyfill"
    "Lit"
    "Lit.React"
    "Lit.Elmish"
    "Lit.Test"
  ]

let ignoreCaseEquals (str1: string) (str2: string) =
    String.Equals(str1, str2, StringComparison.OrdinalIgnoreCase)

match args with
| IgnoreCase "publish"::rest ->
    let target = List.tryHead rest
    let srcDir = fullPath "src"
    let projFiles =
        // [ for file in dirFiles srcDir do
        //     let projDir = srcDir </> file
        //     if isDirectory projDir then
        //         for file in dirFiles projDir do
        //             if file.EndsWith(".fsproj") then
        //                 yield projDir, file ]
        [ for pkg in packages do
            yield (srcDir </> pkg), (pkg + ".fsproj") ]

    for projDir, file in projFiles do
        match target with
        | Some target ->
            if ignoreCaseEquals file.[..(file.Length - 8)] target then
                pushFableNuget (projDir </> file) [] doNothing
        | None -> pushFableNuget (projDir </> file) [] doNothing
| _ -> ()
