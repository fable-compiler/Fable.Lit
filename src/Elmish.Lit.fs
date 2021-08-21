[<RequireQualifiedAccess>]
module Elmish.Lit.Program

open Elmish

let withLit (id: string) (program: Program<'arg, 'model, 'msg, Lit.TemplateResult>): Program<'arg, 'model, 'msg, Lit.TemplateResult> =
    let el = Browser.Dom.document.getElementById(id)
    if isNull el then
        failwith $"Cannot find element with id {id}"

    let setState model dispatch =
        Program.view program model dispatch |> Lit.render el

    Program.withSetState setState program