module React.Lit

open Fable.React
open Browser.Types

let useLit (props: 'Props) (view: 'Props -> Lit.TemplateResult) =
    let container = Hooks.useRef Unchecked.defaultof<Element option>
    Hooks.useEffect((fun () ->
        match container.current with
        | None -> ()
        | Some el -> view props |> Lit.render (el :?> HTMLElement)
    ), [|props|])

    div [ Props.RefValue container ] []
