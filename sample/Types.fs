namespace Sample

type Model =
    { Value: string
      ShowClock: bool
      ShowReact: bool }

type Msg =
    | ChangeValue of string
    | ToggleClock of bool
    | ToggleReact of bool
