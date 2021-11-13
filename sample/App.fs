module Sample.App

open Elmish
open Elmish.HMR
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
    | ToggleClock -> { model with ShowClock = not model.ShowClock }, Cmd.none
    | ToggleReact -> { model with ShowReact = not model.ShowReact }, Cmd.none

let view model dispatch =
    html $"""
      <div style={Styles.verticalContainer}>
        {toggleVisible "React" model.ShowReact true (fun _ -> dispatch ToggleReact)}
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
