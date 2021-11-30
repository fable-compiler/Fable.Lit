namespace Lit.Elmish

open System
open Browser
open Browser.Types
open Elmish
open Lit

type State<'model> =
    | Active of 'model
    | Inactive

type Msg<'msg> =
    | UserMsg of 'msg
    | Stop

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

    // TODO: A helper like this should likely belong to Elmish
    let withTerminationHandler (handler: 'model -> unit) (program: Program<'arg, 'model, 'msg, 'view>): Program<'arg, 'model, 'msg, 'view> =
        program
        |> Program.map id id id id id (fun (criteria, handler2) ->
            criteria, fun model -> handler model; handler2 model)

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

    type HookContext with
        member ctx.useElmish(program: unit -> Program<'arg, 'State, 'Msg, unit>, arg: 'arg) =
            let obs = ctx.useMemo(fun () -> ElmishObservable())

            let state, setState = ctx.useState(fun () ->
                let mapInit init arg =
                    let model, cmd = init arg
                    Active model, Cmd.map UserMsg cmd

                let mapUpdate update msg model =
                    match msg, model with
                    | Stop, _ | _, Inactive -> Inactive, Cmd.none
                    | UserMsg msg, Active model ->
                        let model, cmd = update msg model
                        Active model, Cmd.map UserMsg cmd

                let mapView _view = fun _model _dispatch -> ()

                let mapSetState _setState = obs.SetState

                let mapSubscribe subscribe model =
                    match model with
                    | Active model -> subscribe model |> Cmd.map UserMsg
                    | Inactive -> Cmd.none

                let mapTermination (criteria, handler) =
                    (function Stop -> true | UserMsg msg -> criteria msg),
                    (function
                        | Inactive -> ()
                        | Active model ->
                            handler model
                            // Support "legacy" method of disposing model
                            match box model with
                            | :? System.IDisposable as disp -> disp.Dispose()
                            | _ -> ())

                program()
                |> Program.map mapInit mapUpdate mapView mapSetState mapSubscribe mapTermination
                |> Program.runWith arg

                match obs.Value with
                | None | Some Inactive -> failwith "unexpected"
                | Some(Active v) -> v)

            ctx.useEffectOnce(fun () ->
                Hook.createDisposable(fun () ->
                    obs.Dispatch(Stop)))

            obs.Subscribe(function Inactive -> () | Active state -> setState state)
            state, (UserMsg >> obs.Dispatch)

        member ctx.useElmish(program: unit -> Program<unit, 'State, 'Msg, unit>) =
            ctx.useElmish(program, ())

    type Hook with
        /// Start an [Elmish](https://elmish.github.io/elmish/) model-view-update loop.
        static member inline useElmish(init: 'arg -> ('State * Cmd<'Msg>), update: 'Msg -> 'State -> ('State * Cmd<'Msg>), arg: 'arg): 'State * ('Msg -> unit) =
            Hook.getContext().useElmish((fun () -> Program.mkHidden init update), arg)

        /// Start an [Elmish](https://elmish.github.io/elmish/) model-view-update loop.
        static member inline useElmish(init: unit -> ('State * Cmd<'Msg>), update: 'Msg -> 'State -> ('State * Cmd<'Msg>)): 'State * ('Msg -> unit) =
            Hook.getContext().useElmish(fun () -> Program.mkHidden init update)

        /// Start an [Elmish](https://elmish.github.io/elmish/) model-view-update loop.
        static member inline useElmish(program: unit -> Program<'arg, 'State, 'Msg, unit>, arg: 'arg): 'State * ('Msg -> unit) =
            Hook.getContext().useElmish(program, arg)

        /// Start an [Elmish](https://elmish.github.io/elmish/) model-view-update loop.
        static member inline useElmish(program: unit -> Program<unit, 'State, 'Msg, unit>): 'State * ('Msg -> unit) =
            Hook.getContext().useElmish(program, ())
