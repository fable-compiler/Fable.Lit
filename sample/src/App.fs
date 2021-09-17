module App

open System
open Browser.Types
open Elmish
open Helpers

type Model =
    { Value: string
      ShowClock: bool
      ShowReact: bool }

type Msg =
    | ChangeValue of string
    | ToggleClock
    | ToggleReact

let initialState() =
    { Value = "World"
      ShowClock = true
      ShowReact = true }, Cmd.none

let update msg model =
    match msg with
    | ChangeValue v -> { model with Value = v }, Cmd.none
    | ToggleClock -> { model with ShowClock = not model.ShowClock }, Cmd.none
    | ToggleReact -> { model with ShowReact = not model.ShowReact }, Cmd.none

module ReactLib =
    open Feliz

    [<ReactComponent>]
    let MyComponent showClock =
        let state, setState = React.useState 0
        React.useEffectOnce((fun () ->
            printfn "Initializing React component..."
            React.createDisposable (fun () ->
                printfn "Disposing React component..."
            )
        ))

        Html.div [
            prop.className "card"
            prop.children [
                Html.div [
                    prop.className "card-content"
                    prop.children [
                        Html.div [
                            prop.className "content"
                            prop.children [
                                Html.p $"""I'm a React component. Clock is {if showClock then "visible" else "hidden"}"""
                                Html.button [
                                    prop.className "button"
                                    prop.onClick(fun _ -> setState(state + 1))
                                    prop.text $"""Clicked {state} time{if state = 1 then "" else "s"}!"""
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]

open Lit

let ReactLitComponent =
    React.toLit ReactLib.MyComponent

module Styles =
    let verticalContainer =
        inline_css """.{
            margin-left: 2rem;
            display: flex;
            justify-content: center;
            align-items: center;
            flex-direction: column;
        }"""

    let nameInput (color: string) =
        inline_css $""".{{
            color: {color};
            background-color: lavender;
            padding: 0.25rem;
            font-size: 16px;
            width: 250px;
            margin-bottom: 1rem;
        }}"""

let toggleVisible (txt: string) (isVisible: bool) (isEnabled: bool) (onClick: unit -> unit) =
    html $"""
        <button class="button"
                style="margin: 1rem 0"
                @click={onClick}
                ?disabled={not isEnabled}>
          {if isVisible then Lit.ofText $"Hide {txt}"
           else html $"<strong>Show {txt}</strong>"}
        </button>
    """

// This render function integrates with Elmish and doesn't keep local state
let elmishNameInput value dispatch =
    html $"""
      <div class="content">
        <p>Elmish state: <i>Hello {value}!</i></p>
        <input
          style={Styles.nameInput value}
          value={value}
          @keyup={evTargetValue >> dispatch}>
      </div>
    """

// This function keeps local state and can use hooks
[<HookComponent>]
let LocalNameInput() =
    let value, setValue = Hook.useState "Local"
    let inputRef = Hook.useRef<HTMLInputElement>()

    html $"""
      <div class="content">
        <p>Local state: <i>Hello {value}!</i></p>
        <input
          style={Styles.nameInput value}
          value={value}
          {Lit.refValue inputRef}
          @focus={fun _ ->
            inputRef.value |> Option.iter (fun el -> el.select())}
          @keyup={evTargetValue >> setValue}>
      </div>
    """

[<HookComponent>]
let ClockDisplay model dispatch =
    let transition =
        Hook.useTransition(
            ms = 800,
            cssBefore = inline_css $""".{{
                opacity: 0;
                transform: scale(2) rotate(1turn)
            }}""",
            cssAfter = inline_css $""".{{
                opacity: 0;
                transform: scale(0.1) rotate(-1.5turn)
            }}""",
            onComplete = fun isIn ->
                if model.ShowClock <> isIn then dispatch ToggleClock
        )

    let clockContainer() =
        html $"""
            <div style={transition.css}>
                <my-clock
                    minute-colors="white, red, yellow, purple"
                    hour-color="yellow"></my-clock>
            </div>
        """

    let isButtonEnabled = not transition.active
    html $"""
        <div style="{Styles.verticalContainer}">
            {toggleVisible "Clock" model.ShowClock isButtonEnabled (fun () ->
                transition.trigger(not model.ShowClock))}

            {if transition.out then Lit.nothing else clockContainer()}
        </div>
    """

let view model dispatch =
    html $"""
      <div style={Styles.verticalContainer}>
        {toggleVisible "React" model.ShowReact true (fun () -> dispatch ToggleReact)}
        {if not model.ShowReact then Lit.nothing
         else ReactLitComponent model.ShowClock}

        <br />
        {ClockDisplay model dispatch}

        {elmishNameInput model.Value (ChangeValue >> dispatch)}
        {LocalNameInput()}
      </div>
    """

open Lit.Elmish
open Lit.Elmish.HMR

Clock.register()

Program.mkProgram initialState update view
|> Program.withLit "app-container"
|> Program.run