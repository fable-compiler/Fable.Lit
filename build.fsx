#load "node_modules/fable-publish-utils/PublishUtils.fs"
open PublishUtils

match args with
| IgnoreCase "publish"::_ ->
    pushFableNuget "src/Elmish.Lit.fsproj" [] doNothing
| _ -> ()
