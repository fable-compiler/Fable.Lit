[<RequireQualifiedAccess>]
module Lit.Elmish.Program

open Browser
open Browser.Types
open Elmish
open Lit

/// <summary>
/// Mounts an Elmish loop in the specified element
/// </summary>
let withLitOnElement (el: Element) (program: Program<'arg, 'model, 'msg, Lit.TemplateResult>): Program<'arg, 'model, 'msg, Lit.TemplateResult> =
    let setState model dispatch =
        Program.view program model dispatch |> Lit.render el

    Program.withSetState setState program

/// <summary>
/// Mounts an Elmish loop in the element with the specified id
/// </summary>
/// <remarks>
/// The string passed must be an id of an element in the DOM, this function uses `document.getElementById(id)` to find the element.
/// </remarks>
let withLit (id: string) (program: Program<'arg, 'model, 'msg, Lit.TemplateResult>): Program<'arg, 'model, 'msg, Lit.TemplateResult> =
    let el = document.getElementById(id)
    if isNull el then
        failwith $"Cannot find element with id {id}"

    withLitOnElement el program
