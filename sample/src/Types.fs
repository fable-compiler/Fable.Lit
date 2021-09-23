namespace MyApp

type Model =
    { Value: string
      ShowClock: bool
      ShowReact: bool }

type Msg =
    | ChangeValue of string
    | ToggleClock
    | ToggleReact
