namespace Lit

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.DynamicExtensions
open Browser

module HMRUtil =
    let [<Literal>] GLOBAL_KEY = "__FABLE_LIT_HMR__"
    let [<Emit("import.meta.url")>] importMetaUrl: string = jsNative
    let [<Emit("$0[$1] || ($0[$1] = $2()) ")>] getOrAdd (o: obj) (key: string) (init: unit -> 'T): 'T = jsNative

open HMRUtil

// Vite seems to be very strict on how import.meta.hot is invoked
// so make sure Fable doesn't surround it with parens
type IHot =
    [<Emit("import.meta.hot")>]
    abstract active: bool

    [<Emit("import.meta.hot.data[$1]")>]
    abstract getData: key: string -> obj

    [<Emit("import.meta.hot.data[$1] = $2")>]
    abstract setData: key: string * obj -> unit

    [<Emit("import.meta.hot.decline()")>]
    abstract decline: unit -> unit

    [<Emit("import.meta.hot.accept($1...)")>]
    abstract accept: ?handler: (obj -> unit) -> unit

    [<Emit("import.meta.hot.dispose($1...)")>]
    abstract dispose: (obj -> unit) -> unit

    [<Emit("import.meta.hot.invalidate()")>]
    abstract invalidate: unit -> unit

type IHMRToken =
    interface end

/// Internal use, not meant to be used directly.
type HMRToken() =
    let listeners = Dictionary<string, (obj -> unit)>()

    interface IHMRToken

    member _.Subscribe(key: string, handler: obj -> unit) =
        if listeners.ContainsKey(key) then
            listeners.[key] <- handler
        else
            listeners.Add(key, handler)

    member _.RequestUpdate(newModule: obj) =
        listeners |> Seq.iter (fun kv ->
            kv.Value newModule.[kv.Key])

    static member Get(moduleUrl: string): HMRToken =
        let dic = getOrAdd window GLOBAL_KEY obj
        getOrAdd dic moduleUrl HMRToken

type HMR =
    /// Internal use. If you want to interact with HMR API, see https://vitejs.dev/guide/api-hmr.html
    static member hot: IHot = !!obj()

    /// Call this in module/files you want to activate HMR for when using non-bundling dev servers like [Vite](https://vitejs.dev/) or [Snowpack](https://www.snowpack.dev/).
    ///
    /// The HMR token must be assigned to a **static private** value and shared with HookComponents with `Hook.useHmr`.
    /// The module/file should only expose HookComponents publicly, other members must be private.
    ///
    /// > If you're having issues with HMR you can pass `active=false` to force page reload.
    /// > When compiling in non-debug mode, this has no effect.
    static member inline createToken(?active: bool): IHMRToken =
#if !DEBUG
        unbox ()
#else
        let mutable token = Unchecked.defaultof<_>
        if HMR.hot.active then
            token <- HMRToken.Get(importMetaUrl)
            HMR.hot.accept(fun newModule ->
                if active = Some false then
                    HMR.hot.invalidate()
                else
                    try
                        // Snowpack passes a { module } object, but Vite passes the module directly
                        let newModule = if jsIn "module" newModule then newModule.["module"] else newModule
                        token.RequestUpdate(newModule)
                    with e ->
                        JS.console.warn("[HMR]", e)
                        HMR.hot.invalidate()
            )
        upcast token
#endif