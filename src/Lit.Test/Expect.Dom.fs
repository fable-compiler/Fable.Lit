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

    /// Retries an action every X milliseconds until timeout.
    /// Retries every 200ms with a timeout of 2000ms by default.
    member _.waitUntil(action: unit -> 'T, ?intervalMs: int, ?timeoutMs: int, ?message: string) =
        promise {
            let intervalMs = defaultArg intervalMs 100
            let timeoutMs = defaultArg timeoutMs 2000
            let mutable totalMs = 0
            let mutable success = false
            let mutable res = Unchecked.defaultof<'T>
            while not success do
                try
                    res <- action()
                    success <- true
                    // printfn $"Success in {totalMs}ms"
                with _ ->
                    // printfn $"Error in {totalMs}ms"
                    ()
                if not success then
                    if totalMs >= timeoutMs then
                        failwith (defaultArg message "Timeout!")
                    do! Promise.sleep intervalMs
                    totalMs <- totalMs + intervalMs
            return res
        }

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

/// Creates a div wrapper an puts it in `document.body`, then renders the Lit template
/// on the element, waits until render is complete and returns first element child.
/// When disposed, the wrapper will be removed from `document.body`.
let render (template: TemplateResult) = promise {
    let wrapper = createContainer "div"
    Lit.render wrapper.El template
    // TODO: We should have firstElementChild in Browser.Dom
    let el: HTMLElement = wrapper.El?firstElementChild
    do!
        if not(isNull el.updateComplete) then el.updateComplete
        else
            Promise.create(fun resolve _ ->
                window.requestAnimationFrame(fun _ -> resolve()) |> ignore)
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
            let prefix = $"{el.tagName.ToLower()}.innerText"
            AssertionError.Throw("equal", actual=el.innerText, expected=expected, prefix=prefix)

        Expect.equal expected el.innerText

    /// <summary>
    /// Registers an event listener for a particular event name, use the action callback to make your component fire up the event.
    /// The function will return a promise that resolves once the element dispatches the specified event
    /// </summary>
    /// <param name="eventName">The name of the event to listen to.</param>
    /// <param name="action">A callback to make the element dispatch the event. this callback will be fired immediately after the listener has been registered</param>
    /// <param name="el">Element to monitor for dispatched events.</param>
    let toDispatch eventName (action: unit -> unit) (el: HTMLElement) =
        Promise.create
            (fun resolve reject ->
                el.addEventListener (
                    eventName,
                    fun _ ->
                        resolve true
                )

                action())

    /// <summary>
    /// Registers an event listener for a particular event name, use the action callback to make your component fire up the event.
    /// The function will return a promise that resolves the detail of the custom event once the element dispatches the specified event
    /// </summary>
    /// <param name="eventName">The name of the event to listen to.</param>
    /// <param name="action">A callback to make the element dispatch the event. this callback will be fired immediately after the listener has been registered</param>
    /// <param name="el">Element to monitor for dispatched events.</param>
    /// <returns>The detail that was provided by the custom event </returns>
    let toDispatchCustom<'T> eventName (action: unit -> unit) (el: HTMLElement) =
        Promise.create
            (fun resolve reject ->
                el.addEventListener (
                    eventName,
                    fun (e: Event) ->
                        let custom = e :?> CustomEvent<'T>
                        resolve custom.detail
                )

                action())