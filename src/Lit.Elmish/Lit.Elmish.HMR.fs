// From https://github.com/elmish/hmr
// TODO: Abstract this to a common package? It's the same code as for Elmish.Snabbdom
namespace Lit.Elmish.HMR

open Fable.Core.JsInterop
open Elmish
open Lit

[<RequireQualifiedAccess>]
module Program =

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

        if HMR.hot.active then
            HMR.hot.accept()
            hmrState <- HMR.hot.getData(nameof(hmrState))

        // Because of the way Webpack parses the file, we cannot reassign import.meta.webpackHot to import.meta.hot
        if HMR.webpackHot.active then
            HMR.webpackHot.accept()
            hmrState <- HMR.webpackHot.getData(nameof(hmrState))

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
                if (HMR.hot.active) then
                    HMR.hot.dispose(fun _ ->
                        HMR.hot.setData(nameof(hmrState), hmrState)
                        dispatch Stop
                    )
                if (HMR.webpackHot.active) then
                    HMR.webpackHot.dispose(fun data ->
                        data?(nameof(hmrState)) <- hmrState
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
