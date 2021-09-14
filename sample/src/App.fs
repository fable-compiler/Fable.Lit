module App

open System
open Browser.Types
open Feliz
open Elmish
open Lit
open Lit.Elmish
open Lit.Elmish.HMR
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

module ReactLib =
    open Fable.React
    open Fable.React.Props

    [<ReactComponent>]
    let MyComponent showClock =
        let state = Hooks.useState 0
        Hooks.useEffectDisposable((fun () ->
            printfn "Initializing React component..."
            Hook.createDisposable(fun () ->
                printfn "Disposing React component..."
            )
        ), [||])

        div [ Class "card" ] [
            div [ Class "card-content" ] [
                div [ Class "content" ] [
                    p [] [str $"""I'm a React component. Clock is {if showClock then "visible" else "hidden"}"""]
                    button [
                        Class "button"
                        OnClick (fun _ -> state.update(state.current + 1))
                    ] [ str $"""Clicked {state.current} time{if state.current = 1 then "" else "s"}!"""]
                ]
            ]
        ]

let ReactLitComponent =
    React.toLit ReactLib.MyComponent

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

let itemList model =
    let renderNumber (value: int) =
        html $"""
          <li>Value: <strong>{value}</strong></li>
        """

    let shuffle (li:_ list) =
        let rng = Random()
        let arr = List.toArray li
        let max = (arr.Length - 1)
        let randomSwap (arr:_[]) i =
            let pos = rng.Next(max)
            let tmp = arr.[pos]
            arr.[pos] <- arr.[i]
            arr.[i] <- tmp
            arr

        [|0..max|] |> Array.fold randomSwap arr |> Array.toList

    let items = shuffle [1; 2; 3; 4; 5]
    html $"""
      <div class={Lit.classes ["content", true; "px-4", true; "has-text-primary", DateTime.Now.Second % 2 = 0]}>
        <p>No Key Item List</p>
        <ul>{items |> List.map renderNumber}</ul>
        <p>Keyed Item List</p>
        <ul>{items |> Lit.mapUnique string renderNumber}</ul>
      </div>
    """

[<HookComponent>]
let clockDisplay model dispatch =
    let transitionMs = 500
    let transition = Hook.useTransition(transitionMs)

    let style =
        String.concat " " [
            inline_css $""".{{
                transition-property: all;
                transition-duration: {transitionMs}ms;
            }}"""
            match transition.state with
            | Transition.IsOut -> inline_css """.{ opacity: 0; transform: scale(0.1) }"""
            | Transition.Entering | Transition.IsIn -> inline_css """.{ opacity: 1; transform: scale(1)}"""
            | Transition.Leaving -> inline_css """.{ opacity: 0; transform: scale(2) }"""
        ]

    let clockContainer() =
        html $"""
            <div style={style}>
                <my-clock hour-color="yellow" .reactiveProp={model.ShowReact}></my-clock>"
            </div>
        """

    let isButtonEnabled = not transition.active
    html $"""
        <div style="{Styles.verticalContainer}">
            {toggleVisible "Clock" model.ShowClock isButtonEnabled (fun () ->
                if model.ShowClock then
                    transition.triggerLeave(fun () -> dispatch ToggleClock)
                else
                    dispatch ToggleClock
                    transition.triggerEnter()
            )}

            {if not model.ShowClock then Lit.nothing else clockContainer()}
        </div>
    """

let view model dispatch =
    html $"""
      <div style={Styles.verticalContainer}>
        {toggleVisible "React" model.ShowReact true (fun () -> dispatch ToggleReact)}
        {if not model.ShowReact then Lit.nothing
         else ReactLitComponent model.ShowClock}

        <br />
        {clockDisplay model dispatch}

        {elmishNameInput model.Value (ChangeValue >> dispatch)}
        {LocalNameInput()}
      </div>
    """
    //   {itemList model}

Clock.register()

Program.mkProgram initialState update view
|> Program.withLit "app-container"
|> Program.run