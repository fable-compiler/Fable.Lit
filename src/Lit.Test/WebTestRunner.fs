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
    abstract getSnapshot: name: string -> JS.Promise<SnapshotConfig>
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
        // TODO: Remove whitespace between tags too?
        Regex(@"<\!--.*?-->").Replace(html, "").Trim()

    /// Compares the content string with the snapshot of the given name within the current file.
    /// If the snapshot doesn't exist or tests are run with `--update-snapshots` option the snapshot will just be saved/updated.
    let matchSnapshot (name: string) (content: string) = promise {
        let! config = wtr.getSnapshotConfig()
        if config.updateSnapshots then
            return! wtr.saveSnapshot(name, content)
        else
            return! wtr.compareSnapshot(name, content)
    }

    /// Compares `outerHML` of the element with the snapshot of the given name within the current file.
    /// If the snapshot doesn't exist or tests are run with `--update-snapshots` option the snapshot will just be saved/updated.
    let matchHtmlSnapshot (name: string) (el: HTMLElement) =
        el.outerHTML |> cleanHtml |> matchSnapshot name

    /// Compares `shadowRoot.innerHTML` of the element with the snapshot of the given name within the current file.
    /// If the snapshot doesn't exist or tests are run with `--update-snapshots` option the snapshot will just be saved/updated.
    let matchShadowRootSnapshot (name: string) (el: Element) =
        el.shadowRoot.innerHTML |> cleanHtml |> matchSnapshot name
