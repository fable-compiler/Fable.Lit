module Lit.Test

open System.Text.RegularExpressions
open Fable.Core
open Fable.Core.JsInterop
open Browser.Types

type Queries =
    abstract getByRole: Element * role: string * accessibleNamePattern: string -> Element
    abstract getByText: Element * pattern: string -> Element

[<ImportAll("./queries.min.js")>]
let private queries: Queries = jsNative

type SnapshotConfig =
    abstract updateSnapshots: bool

type WebTestRunner =
    // getSnapshots,
    // removeSnapshot,
    abstract getSnapshotConfig: unit -> JS.Promise<SnapshotConfig>
    [<Emit("$0.getSnapshot({ name: $1 })")>]
    abstract getSnapshot: name: string -> JS.Promise<SnapshotConfig>
    [<Emit("$0.saveSnapshot({ name: $1, content: $2 })")>]
    abstract saveSnapshot: name: string * content: string -> JS.Promise<unit>
    [<Emit("$0.compareSnapshot({ name: $1, content: $2 })")>]
    abstract compareSnapshot: name: string * content: string -> JS.Promise<unit>

[<ImportAll("@web/test-runner-commands")>]
let private wtr: WebTestRunner = jsNative

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

[<ImportMember("@open-wc/testing")>]
let private fixture (template: U2<TemplateResult, string>): JS.Promise<Element> = jsNative

let fixture_html template: JS.Promise<HTMLElement> =
    !^(html template) |> fixture |> Promise.map (fun el -> el :?> HTMLElement)

let fixture_plain_html (template: string): JS.Promise<HTMLElement> =
    fixture !^template |> Promise.map (fun el -> el :?> HTMLElement)

[<Global>]
let describe (msg: string) (suite: unit -> unit): unit = jsNative

[<Global>]
let it (msg: string) (test: unit -> JS.Promise<unit>): unit = jsNative

module Expect =
    let private cleanHtml (html: string) =
        // TODO: Remove whitespace between tags too?
        Regex(@"<\!--.*?-->").Replace(html, "").Trim()

    let matchSnapshot (name: string) (content: string) = promise {
        let! config = wtr.getSnapshotConfig()
        if config.updateSnapshots then
            return! wtr.saveSnapshot(name, content)
        else
            return! wtr.compareSnapshot(name, content)
    }

    let matchHtmlSnapshot (name: string) (el: HTMLElement) =
        el.outerHTML |> cleanHtml |> matchSnapshot name

    let matchShadowRootSnapshot (name: string) (el: Element) =
        el.shadowRoot.innerHTML |> cleanHtml |> matchSnapshot name
