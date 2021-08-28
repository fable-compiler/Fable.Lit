module App

open System
open Browser.Types
open Feliz
open type length
open Elmish
open Lit
open Lit.Feliz
open Lit.Elmish
open Helpers

module R = Fable.React.Standard
module R = Fable.React.Helpers
module P = Fable.React.Props
let ReactHooks = Fable.React.HookBindings.Hooks

type Model =
    { Value: string
      ShowClock: bool
      Clock: Clock.Model }

type Msg =
    | ChangeValue of string
    | ShowClock of bool
    | ClockMsg of Clock.Msg

let initialState() =
    let clock, cmd = Clock.init()
    { Value = "World"
      ShowClock = true
      Clock = clock }, Cmd.map ClockMsg cmd

let update msg model =
    match msg with
    | ChangeValue v -> { model with Value = v }, Cmd.none
    | ShowClock v -> { model with ShowClock = v }, Cmd.none
    | ClockMsg msg ->
        let clock, cmd = Clock.update msg model.Clock
        { model with Clock = clock }, Cmd.map ClockMsg cmd

let buttonFeliz (model: Model) dispatch =
    Feliz.toLit <| Html.button [
        Attr.className "button"
        Ev.onClick (fun _ -> not model.ShowClock |> ShowClock |> dispatch)

        // This doesn't work, template is only built once and cached
        // if model.ShowClock then
        //     Html.text "Hide clock"
        // else
        //     Html.strong "Show clock"

        // Do this instead
        Feliz.ofLit <|
            if model.ShowClock then
                ofText "Hide clock"
            else
                Feliz.toLit <| Html.strong "Show clock"

        // Alternatively you can just embed a Lit template in a Feliz node
        // Note `text` is content, not an element, so it's ok if it appears in a condition
        // if model.ShowClock then
        //     Html.text "Hide clock"
        // else
        //     Feliz.lit_html $"""<strong>Show clock</strong>"""
    ]

let buttonLit (model: Model) dispatch =
    html $"""
        <button class="button" @click={fun _ -> not model.ShowClock |> ShowClock |> dispatch}>
          {if model.ShowClock then "Hide clock" else "Show clock"}
        </button>
    """

module Styles =
    let verticalContainer = [
        Css.marginLeft(rem 2)
        Css.displayFlex
        Css.justifyContentCenter
        Css.alignItemsCenter
        Css.flexDirectionColumn
    ]

    let nameInput = [
      Css.padding(rem 0.25)
      Css.fontSize(px 16)
      Css.width(px 250)
      Css.marginBottom(rem 1)
    ]

let nameInput value dispatch =
    html $"""
      <div style={Feliz.styles Styles.verticalContainer}>
        <input
          style={Feliz.styles Styles.nameInput}
          value={value}
          @keyup={fun (ev: Event) ->
            ev.target.Value |> dispatch}>

        <span>Hello {value}!</span>
      </div>
    """

[<HookComponent>]
let NameInputComponent() =
    let value, setValue = Hook.useState "Local"
    let inputRef = Hook.useRef<HTMLInputElement>()

    html $"""
      <div style={Feliz.styles Styles.verticalContainer}>
        <input
          style={Feliz.styles Styles.nameInput}
          value={value}
          {refValue inputRef}
          @focus={fun _ ->
            inputRef.value |> Option.iter (fun el -> el.select())}
          @keyup={fun (ev: Event) ->
            ev.target.Value |> setValue}>

        <span>Hello {value}!</span>
      </div>
    """

let itemList model =
    let renderNumber (value: int) =
        html $"""
          <li>Value: <strong>{value}</strong></li>
        """

    let shuffle (li:_ list) =
        let rng = new Random()
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
      <div class={classes ["content", true; "px-4", true; "has-text-primary", DateTime.Now.Second % 2 = 0]}>
        <p>No Key Item List</p>
        <ul>{items |> List.map renderNumber}</ul>
        <p>Keyed Item List</p>
        <ul>{items |> ofSeqWithId string renderNumber}</ul>
      </div>
    """

[<ReactComponent>]
let ReactComponent (dt: DateTime) =
    let state = ReactHooks.useState 0
    R.fragment [] [
        R.p [] [R.str $"""I'm a React component"""]
        R.p [] [R.str $"""Time is {dt.ToString("HH:mm:ss")}"""]
        R.button [
            P.Class "button"
            P.OnClick (fun _ -> state.update(state.current + 1))
        ] [ R.str $"Clicked {state.current} time(s)!"]
    ]

let ReactLitComponent = React.toLit ReactComponent

let view model dispatch =
    html $"""
      {ReactLitComponent model.Clock.CurrentTime}
      <div style={Feliz.styles Styles.verticalContainer}>
        {buttonFeliz model dispatch}
        {if model.ShowClock
         then Clock.view model.Clock (ClockMsg >> dispatch)
         else nothing}
      </div>
      {NameInputComponent()}
    """
    //   {nameInput model.Value (ChangeValue >> dispatch)}
    //   {itemList model}

Program.mkProgram initialState update view
|> Program.withSubscription (fun _ -> Cmd.ofSub(fun d -> Clock.subscribe (ClockMsg >> d)))
|> Program.withLit "app-container"
|> Program.run