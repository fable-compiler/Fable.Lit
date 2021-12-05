module Sample.App

open Elmish
// open Elmish.HMR
open Lit
open Lit.Elmish
open Components

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

        <button class="button"
            style="margin: 1rem 0"
            @click={Ev(fun _ -> not model.ShowReact |> ToggleReact |> dispatch)}>
            {if model.ShowReact then "Hide" else "Show"} React
        </button>

        {if not model.ShowReact then Lit.nothing
         else ReactLitComponent model.ShowClock}

        <br />

        {elmishNameInput model.Value (ChangeValue >> dispatch)}
        {LocalNameInput()}

        {ClockDisplay model dispatch}
      </div>
    """

Program.mkProgram init update view
|> Program.withLit "app-container"
|> Program.run
