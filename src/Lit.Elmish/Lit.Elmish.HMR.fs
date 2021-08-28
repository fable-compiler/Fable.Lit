// From https://github.com/elmish/hmr
// TODO: Abstract this to a common package? It's the same code as for Elmish.Snabbdom
namespace Lit.Elmish.HMR

open Fable.Core
open Fable.Core.JsInterop
open Browser
open Elmish

module Bindings =
    [<StringEnum>]
    type Status =
        /// The process is waiting for a call to check (see below)
        | Idle
        /// The process is checking for updates
        | Check
        /// The process is getting ready for the update (e.g. downloading the updated module)
        | Prepare
        /// The update is prepared and available
        | Ready
        /// The process is calling the dispose handlers on the modules that will be replaced
        | Dispose
        /// The process is calling the accept handlers and re-executing self-accepted modules
        | Apply
        /// An update was aborted, but the system is still in it's previous state
        | Abort
        /// An update has thrown an exception and the system's state has been compromised
        | Fail

    type ApplyOptions =
        /// Ignore changes made to unaccepted modules.
        abstract ignoreUnaccepted : bool option with get, set
        /// Ignore changes made to declined modules.
        abstract ignoreDeclined : bool option with get, set
        /// Ignore errors throw in accept handlers, error handlers and while reevaluating module.
        abstract ignoreErrored : bool option with get, set
        /// Notifier for declined modules
        abstract onDeclined : (obj -> unit) option with get, set
        /// Notifier for unaccepted modules
        abstract onUnaccepted : (obj -> unit) option with get, set
        /// Notifier for accepted modules
        abstract onAccepted : (obj -> unit) option with get, set
        /// Notifier for disposed modules
        abstract onDisposed : (obj -> unit) option with get, set
        /// Notifier for errors
        abstract onErrored : (obj -> unit) option with get, set


    [<AllowNullLiteral>]
    type IHot =

        /// **Description**
        /// Retrieve the current status of the hot module replacement process.
        /// **Parameters**
        ///
        ///
        /// **Output Type**
        ///   * `Status`
        ///
        /// **Exceptions**
        ///
        abstract status: unit -> Status

        /// **Description**
        /// Accept updates for the given dependencies and fire a callback to react to those updates.
        /// **Parameters**
        ///   * `dependencies` - parameter of type `U2<string list,string>` - Either a string or an array of strings
        ///   * `errorHandler` - parameter of type `(obj -> unit) option` - Function to fire when the dependencies are updated
        /// **Output Type**
        ///   * `unit`
        ///
        /// **Exceptions**
        ///
        abstract accept: dependencies:  U2<string list, string> * ?errorHandler: (obj -> unit) -> unit

        /// **Description**
        /// Accept updates for itself.
        /// **Parameters**
        ///   * `errorHandler` - parameter of type `(obj -> unit) option` - Function to fire when the dependencies are updated
        ///
        /// **Output Type**
        ///   * `unit`
        ///
        /// **Exceptions**
        ///
        abstract accept: ?errorHandler: (obj -> unit) -> unit

        /// **Description**
        /// Reject updates for the given dependencies forcing the update to fail with a 'decline' code.
        /// **Parameters**
        ///   * `dependencies` - parameter of type `U2<string list,string>` - Either a string or an array of strings
        ///
        /// **Output Type**
        ///   * `unit`
        ///
        /// **Exceptions**
        ///
        abstract decline: dependencies:  U2<string list, string> -> unit

        /// **Description**
        /// Reject updates for itself.
        /// **Parameters**
        ///
        ///
        /// **Output Type**
        ///   * `unit`
        ///
        /// **Exceptions**
        ///
        abstract decline: unit -> unit

        /// **Description**
        /// Add a handler which is executed when the current module code is replaced.
        /// This should be used to remove any persistent resource you have claimed or created.
        /// If you want to transfer state to the updated module, add it to given `data` parameter.
        /// This object will be available at `module.hot.data` after the update.
        /// **Parameters**
        ///   * `data` - parameter of type `obj`
        ///
        /// **Output Type**
        ///   * `unit`
        ///
        /// **Exceptions**
        ///
        abstract dispose: data: obj -> unit

        /// **Description**
        /// Add a handler which is executed when the current module code is replaced.
        /// This should be used to remove any persistent resource you have claimed or created.
        /// If you want to transfer state to the updated module, add it to given `data` parameter.
        /// This object will be available at `module.hot.data` after the update.
        /// **Parameters**
        ///   * `handler` - parameter of type `obj -> unit`
        ///
        /// **Output Type**
        ///   * `unit`
        ///
        /// **Exceptions**
        ///
        abstract addDisposeHandler: handler: (obj -> unit) -> unit

        /// **Description**
        /// Remove the callback added via `dispose` or `addDisposeHandler`.
        /// **Parameters**
        ///   * `callback` - parameter of type `obj -> unit`
        ///
        /// **Output Type**
        ///   * `unit`
        ///
        /// **Exceptions**
        ///
        abstract removeDisposeHandler: callback: (obj -> unit) -> unit

        /// **Description**
        /// Test all loaded modules for updates and, if updates exist, `apply` them.
        /// **Parameters**
        ///   * `autoApply` - parameter of type `U2<bool,ApplyOptions>`
        ///
        /// **Output Type**
        ///   * `JS.Promise<obj>`
        ///
        /// **Exceptions**
        ///
        abstract check: autoApply: U2<bool, ApplyOptions> -> JS.Promise<obj>

        /// **Description**
        /// Continue the update process (as long as `module.hot.status() === 'ready'`).
        /// **Parameters**
        ///   * `options` - parameter of type `U2<bool,ApplyOptions>`
        ///
        /// **Output Type**
        ///   * `JS.Promise<obj>`
        ///
        /// **Exceptions**
        ///
        abstract apply: options : ApplyOptions -> JS.Promise<obj>

        /// **Description**
        /// Register a function to listen for changes in `status`.
        /// **Parameters**
        ///
        ///
        /// **Output Type**
        ///   * `unit`
        ///
        /// **Exceptions**
        ///
        abstract addStatusHandler: (obj -> unit) -> unit

        /// **Description**
        /// Remove a registered status handler.
        /// **Parameters**
        ///   * `callback` - parameter of type `obj -> unit`
        ///
        /// **Output Type**
        ///   * `unit`
        ///
        /// **Exceptions**
        ///
        abstract removeStatusHandler: callback: (obj -> unit) -> unit

    type IModule =
        abstract hot: IHot with get, set

    let [<Global("module")>] ``module`` : IModule = jsNative

module HMR = Bindings

[<RequireQualifiedAccess>]
module Program =

    module Internal =
        type Platform =
            | Browser

        let platform =
            Browser

        let tryRestoreState (hot : HMR.IHot) =
            match platform with
            | Browser ->
                let data = hot?data
                if not (isNull data) && not (isNull data?hmrState) then
                    Some data?hmrState
                else
                    None

        let saveState (data : obj) (hmrState : obj) =
            match platform with
            | Browser ->
                data?hmrState <- hmrState

    type Msg<'msg> =
        | UserMsg of 'msg
        | Stop

    type Model<'model> =
        | Inactive
        | Active of 'model

    /// Start the dispatch loop with `'arg` for the init() function.
    let inline runWith (arg: 'arg) (program: Program<'arg, 'model, 'msg, 'view>) =
#if !DEBUG
        Program.runWith arg program
#else
        let mutable hmrState : obj = null
        let hot = HMR.``module``.hot

        if not (isNull hot) then
            window?Elmish_HMR_Count <-
                if isNull window?Elmish_HMR_Count then
                    0
                else
                    window?Elmish_HMR_Count + 1

            hot.accept() |> ignore

            match Internal.tryRestoreState hot with
            | Some previousState ->
                hmrState <- previousState
            | None -> ()

        let map (model, cmd) =
            model, cmd |> Cmd.map UserMsg

        let mapUpdate update (msg : Msg<'msg>) (model : Model<'model>) =
            let newModel,cmd =
                match msg with
                    | UserMsg msg ->
                        match model with
                        | Inactive -> model, Cmd.none
                        | Active userModel ->
                            let newModel, cmd = update msg userModel
                            Active newModel, cmd

                    | Stop ->
                        Inactive, Cmd.none
                    |> map

            hmrState <- newModel
            newModel,cmd

        let createModel (model, cmd) =
            Active model, cmd

        let mapInit init =
            if isNull hmrState then
                init >> map >> createModel
            else
                (fun _ -> unbox<Model<_>> hmrState, Cmd.none)

        let mapSetState setState (model : Model<'model>) dispatch =
            match model with
            | Inactive -> ()
            | Active userModel ->
                setState userModel (UserMsg >> dispatch)

        let hmrSubscription =
            let handler dispatch =
                if not (isNull hot) then
                    hot.dispose(fun data ->
                        Internal.saveState data hmrState

                        dispatch Stop
                    )
            [ handler ]

        let mapSubscribe subscribe model =
            match model with
            | Inactive -> Cmd.none
            | Active userModel ->
                Cmd.batch [ subscribe userModel |> Cmd.map UserMsg
                            hmrSubscription ]

        let mapView view =
            // This function will never be executed because we are using a local reference to access `program.view`.
            // For example,
            // ```fs
            // let withReactUnoptimized placeholderId (program: Program<_,_,_,_>) =
            //     let setState model dispatch =
            //         Fable.Import.ReactDom.render(
            //             lazyView2With (fun x y -> obj.ReferenceEquals(x,y)) program.view model dispatch,
            //                                                                  ^-- Here program is coming from the function parameters and not
            //                                                                      from the last program composition used to run the applicaiton
            //             document.getElementById(placeholderId)
            //         )
            //
            //     { program with setState = setState }
            // ```*)
            fun model dispatch ->
                match model with
                | Inactive ->
                    """
You are using HMR and this Elmish application has been marked as inactive.
You should not see this message
                    """
                    |> failwith
                | Active userModel ->
                    view userModel (UserMsg >> dispatch)

        program
        |> Program.map mapInit mapUpdate mapView mapSetState mapSubscribe
        |> Program.runWith arg
#endif

    /// Start the dispatch loop with `unit` for the init() function.
    let inline run (program: Program<unit, 'model, 'msg, 'view>) =
#if !DEBUG
        Program.run program
#else
        runWith () program
#endif
