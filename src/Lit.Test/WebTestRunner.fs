module WebTestRunner

open System.Text.RegularExpressions
open Fable.Core
open Browser.Types

type SnapshotConfig =
    abstract updateSnapshots: bool

type WebTestRunnerBindings =
    // getSnapshots,
    // removeSnapshot,
    abstract getSnapshotConfig: unit -> JS.Promise<SnapshotConfig>
    [<Emit("$0.getSnapshot({ name: $1 })")>]
    abstract getSnapshot: name: string -> JS.Promise<string>
    [<Emit("$0.saveSnapshot({ name: $1, content: $2 })")>]
    abstract saveSnapshot: name: string * content: string -> JS.Promise<unit>
    [<Emit("$0.compareSnapshot({ name: $1, content: $2 })")>]
    abstract compareSnapshot: name: string * content: string -> JS.Promise<unit>

[<Global>]
let describe (msg: string) (suite: unit -> unit): unit = jsNative

[<Global>]
let it (msg: string) (test: unit -> JS.Promise<unit>): unit = jsNative

[<RequireQualifiedAccess>]
module Expect =
    [<ImportAll("@web/test-runner-commands")>]
    let private wtr: WebTestRunnerBindings = jsNative

    let private cleanHtml (html: string) =
        // Lit inserts comments with different values every time, so remove them
        Regex(@"<\!--[\s\S]*?-->").Replace(html, "").Trim()

    /// Compares the content string with the snapshot of the given name within the current file.
    /// If the snapshot doesn't exist or tests are run with `--update-snapshots` option the snapshot will just be saved/updated.
    let matchSnapshot (description: string) (name: string) (content: string) = promise {
        let! config = wtr.getSnapshotConfig()
        let! snapshot =
            if config.updateSnapshots then Promise.lift null
            else wtr.getSnapshot(name)
        if isNull snapshot then
            // Web test runner transforms the snapshot into a data URL to send it to the browser,
            // and this can fail if we don't encode the content
            return! wtr.saveSnapshot(name, JS.encodeURIComponent content)
        else
            // Don't use wtr.compareSnapshot because that will update the snapshot
            // without encoding the content even with a successful match
            return
                if not(snapshot = content) then
                    // Snapshots can be large, so use `brief` argument to hide them in the error message
                    // (Diffing should be displayed correctly)
                    Expect.AssertionError.Throw("match snapshot", description=description, actual=content, expected=snapshot, brief=true)
    }

    /// Compares `outerHML` of the element with the snapshot of the given name within the current file.
    /// If the snapshot doesn't exist or tests are run with `--update-snapshots` option the snapshot will just be saved/updated.
    let matchHtmlSnapshot (name: string) (el: HTMLElement) =
        el.outerHTML |> cleanHtml |> matchSnapshot "outerHTML" name

    /// Compares `shadowRoot.innerHTML` of the element with the snapshot of the given name within the current file.
    /// If the snapshot doesn't exist or tests are run with `--update-snapshots` option the snapshot will just be saved/updated.
    let matchShadowRootSnapshot (name: string) (el: Element) =
        el.shadowRoot.innerHTML |> cleanHtml |> matchSnapshot "shadowRoor" name
