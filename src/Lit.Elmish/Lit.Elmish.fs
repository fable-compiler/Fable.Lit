[<RequireQualifiedAccess>]
module Lit.Elmish.Program

open Browser
open Browser.Types
open Elmish

let withLitOnElement (el: Element) (program: Program<'arg, 'model, 'msg, Lit.TemplateResult>): Program<'arg, 'model, 'msg, Lit.TemplateResult> =
    let setState model dispatch =
        Program.view program model dispatch |> Lit.Api.render el

    Program.withSetState setState program

let withLit (id: string) (program: Program<'arg, 'model, 'msg, Lit.TemplateResult>): Program<'arg, 'model, 'msg, Lit.TemplateResult> =
    let el = document.getElementById(id)
    if isNull el then
        failwith $"Cannot find element with id {id}"

    withLitOnElement el program
