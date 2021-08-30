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

    let mutable _domEl = Unchecked.defaultof<Element>

    member _.className = ""
    member _.renderFn = Unchecked.defaultof<obj -> ReactElement>

    member this.render(props: obj) =
        Lit.html $"""<div class={this.className} {Lit.refFn (fun el ->
            match el with
            | Some el when this.isConnected ->
                _domEl <- el
                let reactEl = this.renderFn props
                ReactDom.render(reactEl, el)
            | _ -> ()
        )}></div>"""

    member _.disconnected() =
        if not(isNull _domEl) then
            ReactDom.unmountComponentAtNode(_domEl) |> ignore

type React =
    static member toLit (reactComponent: 'Props -> ReactElement, ?className: string): 'Props -> TemplateResult =
        emitJsExpr (jsConstructor<ReactDirective>, reactComponent, defaultArg className "")
            "class extends $0 { renderFn = $1; className = $2 }"
        |> LitHtml.directive :?> _

    static member inline ofLit (template: TemplateResult, ?tag: string, ?className: string) =
        let tag = defaultArg tag "div"
        let container = Hooks.useRef Unchecked.defaultof<Element option>
        Hooks.useEffect((fun () ->
            match container.current with
            | None -> ()
            | Some el -> template |> Lit.render (el :?> HTMLElement)
        ))
        domEl tag [
            Props.Class (defaultArg className "")
            Props.RefValue container
        ] []

    static member inline lit_html (s: FormattableString) =
        React.ofLit(Lit.html s)

    static member inline lit_svg (s: FormattableString) =
        React.ofLit(Lit.html s)
