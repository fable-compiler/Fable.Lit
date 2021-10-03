namespace Lit.Elmish

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
    module LitElmishExtensionsUtil =
        let useElmish(ctx: HookContext, program: unit -> Program<unit, 'State, 'Msg, unit>) =
            ctx.useElmish(fun () ->
                let mutable init = Unchecked.defaultof<_>
                let mutable update = Unchecked.defaultof<_>
                let mutable subscribe = Unchecked.defaultof<_>

                // Poor man's way of accessing program's functions
                program() |> Program.map
                    (fun _init -> init <- _init; _init)
                    (fun _update -> update <- _update; _update)
                    id // view
                    id // setState
                    (fun _subscribe -> subscribe <- _subscribe; _subscribe)
                    |> ignore

                let init() =
                    let model, cmd1 = init()
                    let cmd2 = subscribe model
                    model, cmd1 @ cmd2

                init, update)

    open LitElmishExtensionsUtil

    type Hook with
        static member inline useElmish(program: Program<unit, 'State, 'Msg, unit>): 'State * ('Msg -> unit) =
            useElmish(Hook.getContext(), fun () -> program)

        static member inline useElmish(program: Lazy<Program<unit, 'State, 'Msg, unit>>): 'State * ('Msg -> unit) =
            useElmish(Hook.getContext(), program.Force)

        static member inline useElmish(program: unit -> Program<unit, 'State, 'Msg, unit>): 'State * ('Msg -> unit) =
            useElmish(Hook.getContext(), program)
