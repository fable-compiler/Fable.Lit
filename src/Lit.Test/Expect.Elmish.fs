module Expect.Elmish

open System
open Elmish
open Lit
open Expect.Dom

type ElmishContainer<'Model> =
    inherit Container
    abstract Model: 'Model

type ElmishDispatcher<'Model, 'Msg> =
    inherit IDisposable
    abstract Model: 'Model
    abstract Dispatch: 'Msg -> unit

module Program =
    let private disposeModel (model: obj) =
        match model with
        | :? IDisposable as disp -> disp.Dispose()
        | _ -> ()

    /// Mounts an element to the DOM to render the Elmish app and returns the container
    /// with an extra property to retrieve the model.
    let mountAndTestWith (arg: 'arg) (program: Program<'arg, 'model, 'msg, Lit.TemplateResult>) = promise {
        let mutable model = Unchecked.defaultof<_>
        let! container = render_html $"<div></div>"

        let setState model' dispatch =
            model <- model'
            Program.view program model dispatch |> Lit.render container.El

        Program.withSetState setState program
        |> Program.runWith arg

        return
            { new ElmishContainer<'model> with
                member _.Dispose() =
                    container.Dispose()
                    disposeModel model
                member _.El = container.El
                member _.Model = model
            }
    }

    /// Mounts an element to the DOM to render the Elmish app and returns the container
    /// with an extra property to retrieve the model.
    let mountAndTest (program: Program<unit, 'model, 'msg, Lit.TemplateResult>) =
        mountAndTestWith () program

    /// Returns a handler to retrieve the model and dispatch messages
    let testWith (arg: 'arg) (program: Program<'arg, 'model, 'msg, unit>) =
        let mutable model = Unchecked.defaultof<_>
        let mutable dispatch = Unchecked.defaultof<_>

        let setState model' dispatch' =
            model <- model'
            dispatch <- dispatch'

        Program.withSetState setState program
        |> Program.runWith arg

        { new ElmishDispatcher<'model, 'msg> with
            member _.Dispose() = disposeModel model
            member _.Model = model
            member _.Dispatch msg = dispatch msg
        }

    /// Returns a handler to retrieve the model and dispatch messages
    let test (program: Program<unit, 'model, 'msg, unit>) =
        testWith () program
