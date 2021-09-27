module Expect.Dom

open System
open Fable.Core
open Fable.Core.JsInterop
open Browser
open Browser.Types
open Lit

type Queries =
    abstract getByRole: Element * role: string * accessibleNamePattern: string -> Element
    abstract getByText: Element * pattern: string -> Element

[<ImportAll("./queries.min.js")>]
let private queries: Queries = jsNative

type Element with
    member this.cast<'El when 'El :> Element>() =
        this :?> 'El

    member this.asInput =
        this.cast<HTMLInputElement>()

    member this.asButton =
        this.cast<HTMLButtonElement>()

    member this.asHTML =
        this.cast<HTMLElement>()

    /// Runs querySelector and throw error if nothing is found
    member this.getSelector(selector: string) =
        match this.querySelector(selector) with
        | null -> failwith $"""Cannot find element with selector "{selector}"."""
        | v -> v

    /// Runs querySelector and returns None if nothing is found
    member this.tryGetSelector(selector: string) =
        this.querySelector(selector) |> Option.ofObj

    /// Return first child that has given role and whose accessible name matches the pattern, or throw error
    /// Pattern becomes an ignore-case regular expression.
    member this.getByRole(role: string, accessibleNamePattern: string) =
        queries.getByRole(this, role, accessibleNamePattern)

    /// Same as getByRole("button", accessibleNamePattern).
    member this.getButton(accessibleNamePattern: string) =
        queries.getByRole(this, "button", accessibleNamePattern) :?> HTMLButtonElement

    /// Same as getByRole("checkbox", accessibleNamePattern).
    /// Matches `input` elements of type "text" or `checkbox`
    member this.getCheckbox(accessibleNamePattern: string) =
        queries.getByRole(this, "checkbox", accessibleNamePattern) :?> HTMLInputElement

    /// Same as getByRole("textbox", accessibleNamePattern).
    /// Matches `input` elements of type "text" or `textarea`.
    member this.getTextInput(accessibleNamePattern: string) =
        queries.getByRole(this, "textbox", accessibleNamePattern) :?> HTMLInputElement

    /// Return first text node child with text matching given pattern, or throw error.
    /// Pattern becomes an ignore-case regular expression.
    member this.getByText(pattern: string) =
        queries.getByText(this, pattern) :?> HTMLElement

type Container =
    inherit IDisposable
    abstract El: HTMLElement

/// Creates an HTML element with the specified tag an puts it in `document.body`.
/// When disposed, the element will be removed from `document.body`.
let createContainer (tagName: string) =
    let el = document.createElement(tagName)
    document.body.appendChild(el) |> ignore
    { new Container with
        member _.El = el
        member _.Dispose() = document.body.removeChild(el) |> ignore }

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

/// Creates a div wrapper an puts it in `document.body`, then renders the Lit template
/// on the element, waits until render is complete and returns first element child.
/// When disposed, the wrapper will be removed from `document.body`.
let render (template: TemplateResult) = promise {
    let wrapper = createContainer "div"
    Lit.render wrapper.El template
    // TODO: We should have firstElementChild in Browser.Dom
    let el: HTMLElement = wrapper.El?firstElementChild
    do! elementUpdated el
    return { new Container with
        member _.El = el
        member _.Dispose() = wrapper.Dispose() }
}

/// Creates a div wrapper an puts it in `document.body`, then renders the HTML template
/// on the element with Lit, waits until render is complete and returns first element child.
/// When disposed, the wrapper will be removed from `document.body`.
let render_html (template: FormattableString) =
    html template |> render

[<RequireQualifiedAccess>]
module Expect =
    /// <summary>
    /// Checks the text content of an element
    /// </summary>
    let innerText (expected: string) (el: Element) =
        let el = el :?> HTMLElement
        if not(el.innerText = expected) then
            let description = $"{el.tagName.ToLower()}.innerText"
            AssertionError.Throw("equal", description=description, actual=el.innerText, expected=expected)

    /// <summary>
    /// Registers an event listener for a particular event name, use the action callback to make your component fire up the event.
    /// The function will return a promise that resolves once the element dispatches the specified event
    /// </summary>
    /// <param name="eventName">The name of the event to listen to.</param>
    /// <param name="action">A callback to make the element dispatch the event. this callback will be fired immediately after the listener has been registered</param>
    /// <param name="el">Element to monitor for dispatched events.</param>
    let dispatch eventName (action: unit -> unit) (el: HTMLElement) =
        Promise.create
            (fun resolve reject ->
                el.addEventListener (
                    eventName,
                    fun _ ->
                        resolve()
                )

                action())
        // The event should be fired immediately so we set a small timeout
        |> Expect.beforeTimeout $"dispatch {eventName}" 100

    /// <summary>
    /// Registers an event listener for a particular event name, use the action callback to make your component fire up the event.
    /// The function will return a promise that resolves the detail of the custom event once the element dispatches the specified event
    /// </summary>
    /// <param name="eventName">The name of the event to listen to.</param>
    /// <param name="action">A callback to make the element dispatch the event. this callback will be fired immediately after the listener has been registered</param>
    /// <param name="el">Element to monitor for dispatched events.</param>
    /// <returns>The detail that was provided by the custom event </returns>
    let dispatchCustom<'T> eventName (action: unit -> unit) (el: HTMLElement) =
        Promise.create
            (fun resolve reject ->
                el.addEventListener (
                    eventName,
                    fun (e: Event) ->
                        let custom = e :?> CustomEvent<'T>
                        resolve custom.detail
                )

                action())
        // The event should be fired immediately so we set a small timeout
        |> Expect.beforeTimeout $"dispatch {eventName}" 100
