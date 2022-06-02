namespace Lit.Elmish

open System
open Browser
open Browser.Types
open Elmish
open Lit

[<RequireQualifiedAccess>]
module Program =
    /// Creates an elmish program without a view function.
    /// Useful for testing or using the program with `Hook.useElmish`.
    let mkHidden init update =
        let view _ _ = ()
        Program.mkProgram init update view

    /// <summary>
    /// Mounts an Elmish loop in the specified element
    /// </summary>
    let withLitOnElement (el: Element) (program: Program<'arg, 'model, 'msg, Lit.TemplateResult>): Program<'arg, 'model, 'msg, Lit.TemplateResult> =
        let setState model dispatch =
            Program.view program model dispatch |> Lit.render el

        Program.withSetState setState program

    /// <summary>
    /// Mounts an Elmish loop in the element with the specified id
    /// </summary>
    /// <remarks>
    /// The string passed must be an id of an element in the DOM, this function uses `document.getElementById(id)` to find the element.
    /// </remarks>
    let withLit (id: string) (program: Program<'arg, 'model, 'msg, Lit.TemplateResult>): Program<'arg, 'model, 'msg, Lit.TemplateResult> =
        let el = document.getElementById(id)
        if isNull el then
            failwith $"Cannot find element with id {id}"

        withLitOnElement el program

[<AutoOpen>]
module LitElmishExtensions =
    type ElmishObservable<'State, 'Msg>() =
        let mutable state: 'State option = None
        let mutable listener: ('State -> unit) option = None
        let mutable dispatcher: ('Msg -> unit) option = None

        member _.Value = state

        member _.SetState (model: 'State) (dispatch: 'Msg -> unit) =
            state <- Some model
            dispatcher <- Some dispatch
            match listener with
            | None -> ()
            | Some listener -> listener model

        member _.Dispatch(msg) =
            match dispatcher with
            | None -> () // Error?
            | Some dispatch -> dispatch msg

        member _.Subscribe(f) =
            match listener with
            | Some _ -> ()
            | None -> listener <- Some f

    [<Fable.Core.AttachMembers>]
    type ElmishController<'State, 'Msg>(host, init, update) as this =
        inherit ReactiveController(host)
        let obs: ElmishObservable<'State, 'Msg> = ElmishObservable()

        member val state = unbox obs.Value with get, set

        member _.dispatch = obs.Dispatch

        override _.hostConnected() =
            Program.mkHidden init update
            |> Program.withSetState obs.SetState
            |> Program.run

            match obs.Value with
            | None -> failwith "Not Started"
            | Some value -> this.state <- value

            obs.Subscribe(fun state ->
                this.state <- state
                host.requestUpdate ())

        override _.hostDisconnected() =
            match box obs.Value with
            | :? System.IDisposable as disp -> disp.Dispose()
            | _ -> ()


    let useElmish(ctx: HookContext, program: unit -> Program<unit, 'State, 'Msg, unit>) =
        let obs = ctx.useMemo(fun () -> ElmishObservable())

        let state, setState = ctx.useState(fun () ->
            program()
            |> Program.withSetState obs.SetState
            |> Program.run

            match obs.Value with
            | None -> failwith "Elmish program has not initialized"
            | Some v -> v)

        ctx.useEffectOnce(fun () ->
            Hook.createDisposable(fun () ->
                match box state with
                | :? System.IDisposable as disp -> disp.Dispose()
                | _ -> ()))

        obs.Subscribe(setState)
        state, obs.Dispatch

    type Hook with
        /// <summary>
        /// Start an [Elmish](https://elmish.github.io/elmish/) model-view-update loop.
        /// </summary>
        /// <example>
        ///      type State = { counter: int }
        ///
        ///      type Msg = Increment | Decrement
        ///
        ///      let init () = { counter = 0 }
        ///
        ///      let update msg state =
        ///          match msg with
        ///          | Increment -&gt; { state with counter = state.counter + 1 }
        ///          | Decrement -&gt; { state with counter = state.counter - 1 }
        ///
        ///      [&lt;HookComponent>]
        ///      let app () =
        ///          let state, dispatch = Hook.useElmish(init, update)
        ///         html $"""
        ///               &lt;header>Click the counter&lt;/header>
        ///               &lt;div id="count">{state.counter}&lt;/div>
        ///               &lt;button type="button" @click=${fun _ -> dispatch Increment}>
        ///                 Increment
        ///               &lt;/button>
        ///               &lt;button type="button" @click=${fun _ -> dispatch Decrement}>
        ///                   Decrement
        ///                &lt;/button>
        ///              """
        /// </example>
        static member inline useElmish(init: unit -> ('State * Cmd<'Msg>), update: 'Msg -> 'State -> ('State * Cmd<'Msg>)): 'State * ('Msg -> unit) =
            useElmish(Hook.getContext(), fun () -> Program.mkHidden init update)

        static member inline useElmish(program: Program<unit, 'State, 'Msg, unit>): 'State * ('Msg -> unit) =
            useElmish(Hook.getContext(), fun () -> program)

        static member inline useElmish(program: unit -> Program<unit, 'State, 'Msg, unit>): 'State * ('Msg -> unit) =
            useElmish(Hook.getContext(), program)
