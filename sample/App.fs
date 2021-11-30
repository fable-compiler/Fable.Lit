module Sample.App

open Browser
open Elmish
open Elmish.HMR
open Lit
open Lit.Elmish
open Components

let private hmr = HMR.createToken()

Clock.register()

let init() =
    { Value = "World"
      ShowClock = true
      ShowReact = true }, Cmd.none

let update msg model =
    match msg with
    | ChangeValue v -> { model with Value = v }, Cmd.none
    | ToggleClock v -> { model with ShowClock = v }, Cmd.none
    | ToggleReact v -> { model with ShowReact = v }, Cmd.none

let view model dispatch =
    html $"""
      <div class="vertical-container" style="margin-left: 2rem;">

        <!-- {Components.Button("Click me", fun () -> window.alert("Clicked!"))}

        {Components.ModalButtonStyled("This is a modal")}

        <modal-button text="Hohoho" /> -->

        <shadow-button text="I count clicks" />

      </div>
    """

// Program.mkProgram init update view
// |> Program.withLit "app-container"
// |> Program.run

[<LitElement("sample-app")>]
let App() =
    Hook.useHmr(hmr)
    let _ = LitElement.init(fun config ->
        config.useShadowDom <- false
    )
    let model, dispatch = Hook.useElmish(init, update)
    view model dispatch
