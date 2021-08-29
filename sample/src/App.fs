module App

open System
open Browser.Types
open Feliz
open type length
open Elmish
open Lit
open Lit.Feliz
open Lit.Elmish
open Lit.Elmish.HMR
open Helpers

type Model =
    { Value: string
      ShowClock: bool }

type Msg =
    | ChangeValue of string
    | ShowClock of bool

let initialState() =
    { Value = "World"
      ShowClock = true }, Cmd.none

let update msg model =
    match msg with
    | ChangeValue v -> { model with Value = v }, Cmd.none
    | ShowClock v -> { model with ShowClock = v }, Cmd.none

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

module ReactLib =
    open Fable.React
    open Fable.React.Props

    [<ReactComponent>]
    let MyComponent showClock =
        let state = Hooks.useState 0
        fragment [] [
            p [] [str $"""I'm a React component"""]
            p [] [str $"""Clock is {if showClock then "visible" else "hidden"}"""]
            button [
                Class "button"
                OnClick (fun _ -> state.update(state.current + 1))
            ] [ str $"Clicked {state.current} time(s)!"]
        ]

let ReactLitComponent =
    React.toLit(ReactLib.MyComponent, className="container")

let buttonLit (model: Model) dispatch =
    let strong txt =
        html $"<strong>{txt}</strong>"

    html $"""
        <button class="button" @click={fun _ -> not model.ShowClock |> ShowClock |> dispatch}>
          {if model.ShowClock then Lit.ofText "Hide clock" else strong "Show clock"}
        </button>
    """

// This is the equivalent to the function above using Feliz.Engine
// Note we cannot change nested nodes dynamically (except for text and css) unless we convert them to lit first
let buttonFeliz (model: Model) dispatch =
    Feliz.toLit <| Html.button [
        Css.marginBottom(rem 1)
        Attr.className "button"
        Ev.onClick (fun _ -> not model.ShowClock |> ShowClock |> dispatch)

        // This doesn't work, template is only built once and cached
        // if model.ShowClock then Html.text "Hide clock"
        // else Html.strong "Show clock"

        // Do this instead
        Feliz.ofLit <|
            if model.ShowClock then Lit.ofText "Hide clock"
            else Feliz.toLit <| Html.strong "Show clock"

        // Alternatively you can just embed a Lit template in a Feliz node
        // Note text and css nodes are considered values and can appear in a condition

        // if model.ShowClock then Html.text "Hide clock"
        // else Feliz.lit_html $"""<strong>Show clock</strong>"""
    ]

// This render function integrates with Elmish and doesn't keep local state
let nameInput value dispatch =
    html $"""
      <div style={Feliz.styles Styles.verticalContainer}>
        <p>I get my state from Elmish</p>
        <input
          style={Feliz.styles Styles.nameInput}
          value={value}
          @keyup={fun (ev: Event) ->
            ev.target.Value |> dispatch}>

        <span>Hello {value}!</span>
      </div>
    """

// This function keeps local state and can use hooks
[<HookComponent>]
let NameInputComponent() =
    let value, setValue = Hook.useState "Local"
    let inputRef = Hook.useRef<HTMLInputElement>()

    html $"""
      <div style={Feliz.styles (
                      Css.marginTop(rem 1.5)
                      ::Styles.verticalContainer
                  )}>
        <p>I have local state</p>
        <input
          style={Feliz.styles Styles.nameInput}
          value={value}
          {Lit.refValue inputRef}
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
      <div class={Lit.classes ["content", true; "px-4", true; "has-text-primary", DateTime.Now.Second % 2 = 0]}>
        <p>No Key Item List</p>
        <ul>{items |> List.map renderNumber}</ul>
        <p>Keyed Item List</p>
        <ul>{items |> Lit.mapUnique string renderNumber}</ul>
      </div>
    """

let view model dispatch =
    html $"""
      {ReactLitComponent model.ShowClock}

      <div style={Feliz.styles Styles.verticalContainer}>

        {buttonFeliz model dispatch}
        {if model.ShowClock then Clock.Clock() else Lit.nothing}

      </div>

      {nameInput model.Value (ChangeValue >> dispatch)}
      {NameInputComponent()}
    """
    //   {itemList model}

Program.mkProgram initialState update view
|> Program.withLit "app-container"
|> Program.run