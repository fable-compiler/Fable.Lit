namespace Lit

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.React
open Browser.Types
open Lit

[<AttachMembers>]
type ReactDirective() =
    inherit AsyncDirective()

    let mutable _firstRun = true
    let _domElRef = Lit.createRef<Element option>()

    member _.className = ""
    member _.renderFn = Unchecked.defaultof<obj -> ReactElement>

    member this.renderReact(props) =
        let reactEl = this.renderFn props
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
        Lit.html $"""<div class={this.className} {Lit.refValue _domElRef}></div>"""

type React =
    static member toLit (reactComponent: 'Props -> ReactElement, ?className: string): 'Props -> TemplateResult =
        emitJsExpr (jsConstructor<ReactDirective>, reactComponent, defaultArg className "")
            "class extends $0 { renderFn = $1; className = $2 }"
        |> LitHtml.directive :?> _

    static member inline ofLit (template: TemplateResult, ?tag: string) =
        let tag = defaultArg tag "div"
        let container = Hooks.useRef Unchecked.defaultof<Element option>
        Hooks.useEffect((fun () ->
            match container.current with
            | None -> ()
            | Some el -> template |> Lit.render (el :?> HTMLElement)
        ))
        domEl tag [ Props.RefValue container ] []

    static member inline lit_html (s: FormattableString) =
        React.ofLit(Lit.html s)

    static member inline lit_svg (s: FormattableString) =
        React.ofLit(Lit.html s)
