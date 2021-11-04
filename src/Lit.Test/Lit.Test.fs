module Lit.Test

open System
open Fable.Core
open Fable.Core.JsInterop
open Browser
open Browser.Types
open Expect.Dom
open Lit

/// For LitElements, awaits `el.updateComplete`.
/// For rest of elements, awaits `window.requestAnimationFrame`.
/// Ensures that ShadyDOM finished its job if available.
let elementUpdated (el: Element) =
    match box el with
    | :? LitElement as el -> el.updateComplete
    | _ -> Promise.create(fun resolve _ ->
        window.requestAnimationFrame(fun _ -> resolve()) |> ignore)
    // Check if ShadyDOM polyfill is being used
    // https://github.com/webcomponents/polyfills/tree/master/packages/shadydom
    |> Promise.map (fun () ->
        emitJsStatement () """
            if (window.ShadyDOM && typeof window.ShadyDOM.flush === 'function') {
                window.ShadyDOM.flush();
            }"""
    )

/// Clicks a button and awaits for the element to be updated
let click (el: Element) (button: HTMLButtonElement) =
    button.click()
    elementUpdated el

/// Creates a div container, puts it in `document.body`, renders the template onto it,
/// waits until render is complete and returns first element child.
/// When disposed, the container will be removed from `document.body`.
let render (template: TemplateResult): JS.Promise<Container> = promise {
    let container = Container.New()
    Lit.render container.El template
    // TODO: We should have firstElementChild in Browser.Dom
    let el: HTMLElement = container.El?firstElementChild
    do! elementUpdated el
    return { new Container with
                member _.El = el
                member _.Dispose() = container.Dispose() }
}

/// Creates a div container, puts it in `document.body`, renders the template onto it,
/// waits until render is complete and returns first element child.
/// When disposed, the container will be removed from `document.body`.
let render_html (template: FormattableString) =
    html template |> render

[<RequireQualifiedAccess>]
module Program =
    /// Mounts an element to the DOM to render the Elmish app and returns the container
    /// with an extra property to retrieve the model.
    let mountAndTestWith (arg: 'arg) (program: Elmish.Program<'arg, 'model, 'msg, Lit.TemplateResult>) =
        Expect.Elmish.Program.mountAndTestWith Lit.render arg program

    /// Mounts an element to the DOM to render the Elmish app and returns the container
    /// with an extra property to retrieve the model.
    let mountAndTest (program: Elmish.Program<unit, 'model, 'msg, Lit.TemplateResult>) =
        Expect.Elmish.Program.mountAndTest Lit.render program
