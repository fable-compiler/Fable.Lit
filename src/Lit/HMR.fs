namespace Lit

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.DynamicExtensions
open Browser

module HMRTypes =
    let [<Literal>] GLOBAL_KEY = "__FABLE_LIT_HMR__"
    let [<Emit("import.meta.url")>] importMetaUrl: string = jsNative
    let [<Emit("$0[$1] || ($0[$1] = $2()) ")>] getOrAdd (o: obj) (key: string) (init: unit -> 'T): 'T = jsNative

    // Vite seems to be very strict on how import.meta.hot is invoked
    // so make sure Fable doesn't surround it with parens
    type IHot =
        [<Emit("import.meta.hot")>]
        abstract active: bool

        [<Emit("import.meta.hot.data[$1]")>]
        abstract getData: key: string -> obj

        [<Emit("import.meta.hot.data[$1] = $2")>]
        abstract setData: key: string * obj -> unit

        [<Emit("import.meta.hot.accept($1...)")>]
        abstract accept: ?handler: (obj -> unit) -> unit

        [<Emit("import.meta.hot.dispose($1...)")>]
        abstract dispose: (obj -> unit) -> unit

        [<Emit("import.meta.hot.invalidate()")>]
        abstract invalidate: unit -> unit

    // Webpack uses import.meta.webpackHot instead :/
    type IWebpackHot =
        [<Emit("import.meta.webpackHot")>]
        abstract active: bool

        [<Emit("import.meta.webpackHot.data ? import.meta.webpackHot.data[$1] : null")>]
        abstract getData: key: string -> obj

        [<Emit("import.meta.webpackHot.accept($1...)")>]
        abstract accept: ?handler: (obj -> unit) -> unit

        [<Emit("import.meta.webpackHot.dispose($1...)")>]
        abstract dispose: (obj -> unit) -> unit

        [<Emit("import.meta.webpackHot.invalidate()")>]
        abstract invalidate: unit -> unit

    type IHMRToken =
        interface end

    type HMRInfo =
        abstract NewModule: obj
        abstract Data: obj

    /// Internal use, not meant to be used directly.
    type HMRToken() =
        let listeners = Dictionary<Guid, _>()

        interface IHMRToken

        member _.Subscribe(handler: HMRInfo -> unit) =
            let guid = Guid.NewGuid()
            listeners.Add(guid, handler)
            { new IDisposable with
                member _.Dispose() =
                    listeners.Remove(guid)
                    |> ignore }

        member _.RequestUpdate(newModule: obj) =
            let data = obj()
            let info =
                { new HMRInfo with
                    member _.NewModule = newModule
                    member _.Data = data
                }
            listeners.Values |> Seq.iter (fun handler -> handler info)

        static member Get(moduleUrl: string): HMRToken =
            // Dev server may add query params to the url
            let moduleUrl =
                match moduleUrl.IndexOf("?") with
                | -1 -> moduleUrl
                | i -> moduleUrl.[..i-1]
            let dic = getOrAdd window GLOBAL_KEY obj
            getOrAdd dic moduleUrl HMRToken

    type HMRSubscriber =
        abstract subscribeHmr: (HMRToken -> unit) option

open HMRTypes

type HMR =
    /// Internal use. If you want to interact with HMR API, see https://vitejs.dev/guide/api-hmr.html
    static member hot: IHot = !!obj()

    /// Internal use. If you want to interact with HMR API, see https://webpack.js.org/api/hot-module-replacement/
    static member webpackHot: IWebpackHot = !!obj()

    [<Emit("{{ $0 ? if (import.meta.hot) { import.meta.hot.accept($1) } : }}", isStatement=true)>]
    static member activateToken(active: bool, accept: obj -> unit): unit = jsNative

    /// Call this in module/files you want to activate HMR for when using non-bundling dev servers like [Vite](https://vitejs.dev/) or [Snowpack](https://www.snowpack.dev/).
    ///
    /// The HMR token must be assigned to a **static private** value and shared with HookComponents with `Hook.useHmr`.
    /// The module/file should only expose HookComponents publicly, other members must be private.
    ///
    /// > If you're having issues with HMR you can pass `active=false` to force page reload.
    /// > When compiling in non-debug mode, this has no effect.
    static member inline createToken(active: bool): IHMRToken =
#if !DEBUG
        unbox ()
#else
        let mutable token = Unchecked.defaultof<_>
        try
            token <- HMRToken.Get(importMetaUrl)
            HMR.activateToken(active, token.RequestUpdate)
        with _ -> ()
        upcast token
#endif