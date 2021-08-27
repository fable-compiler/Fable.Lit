[<RequireQualifiedAccess>]
module React

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.React
open Browser.Types
open Lit

[<AttachMembers>]
type ReactDirective() =
    inherit AsyncDirective()

    let renderFn = Unchecked.defaultof<obj -> ReactElement>

    let mutable _firstRun = true
    let _domElRef = createRef<Element>()
    let _template = html $"""<div {refValue _domElRef}></div>"""

    member _.renderReact(props) =
        let reactEl = renderFn props
        match _domElRef.value with
        | None -> ()
        | Some domEl -> ReactDom.render(reactEl, domEl)

    member this.render(props: obj) =
        if _firstRun then
            _firstRun <- false
            // Let lit-html mount the template on the DOM so we can get the ref
            JS.setTimeout (fun () -> this.renderReact(props)) 0 |> ignore
        else
            this.renderReact(props)
        _template

let toLit (fn: 'Props -> ReactElement): 'Props -> TemplateResult =
    emitJsExpr (jsConstructor<ReactDirective>, fn) "class extends $0 { renderFn = $1 }"
    |> LitHtml.directive :?> _

let inline ofLit (tag: string) (template: TemplateResult) =
    let container = Hooks.useRef Unchecked.defaultof<Element option>
    Hooks.useEffect((fun () ->
        match container.current with
        | None -> ()
        | Some el -> template |> render (el :?> HTMLElement)
    ))
    domEl tag [ Props.RefValue container ] []

let inline lit_html (s: FormattableString) =
    ofLit "div" (html s)

let inline lit_svg (s: FormattableString) =
    ofLit "div" (html s)
