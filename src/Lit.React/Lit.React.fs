[<RequireQualifiedAccess>]
module Lit.React

open System
open Fable.Core.JsInterop
open Fable.React
open Browser.Types

let toLit: ReactElement -> Lit.TemplateResult =
    let directive = importMember "lit-html"
    let render = importMember "react-dom"
    emitJsExpr (directive, render) "$0((reactEl) => (part) => { debugger; $1(reactEl, part) })"

let inline ofLit (tag: string) (template: Lit.TemplateResult) =
    let container = Hooks.useRef Unchecked.defaultof<Element option>
    Hooks.useEffect((fun () ->
        match container.current with
        | None -> ()
        | Some el -> template |> Lit.Api.render (el :?> HTMLElement)
    ))
    domEl tag [ Props.RefValue container ] []

let inline lit_html (s: FormattableString) =
    ofLit "div" (Lit.Api.html s)

let inline lit_svg (s: FormattableString) =
    ofLit "div" (Lit.Api.html s)
