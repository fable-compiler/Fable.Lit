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

let buttonLit (model: Model) dispatch =
    let strong txt =
        html $"""<strong>{txt}</strong>"""

    html $"""
        <button class="button"
                style={ LitHtml.styleMap {| margin = "1rem 0"  |} }
                @click={fun _ -> ToggleReact |> dispatch}>
          {if model.ShowReact then Lit.ofText "Hide React" else strong "Show React"}
        </button>
    """

// This is the equivalent to the function above using Feliz.Engine
// Note we cannot change nested nodes dynamically (except for text and css) unless we convert them to lit first
let buttonFeliz (model: Model) dispatch =
    Feliz.toLit <| Html.button [
        Attr.className "button"
        Css.margin(rem 1, zero)
        Ev.onClick (fun _ -> ToggleClock |> dispatch)

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
      <div class="content">
        <p>Elmish state: <i>Hello {value}!</i></p>
        <input
          style={Feliz.styles Styles.nameInput}
          value={value}
          @keyup={fun (ev: Event) ->
            ev.target.Value |> dispatch}>
      </div>
    """

// This function keeps local state and can use hooks
[<HookComponent>]
let NameInput() =
    let value, setValue = Hook.useState "Local"
    let inputRef = Hook.useRef<HTMLInputElement>()

    html $"""
      <div class="content">
        <p>Local state: <i>Hello {value}!</i></p>
        <input
          style={Feliz.styles Styles.nameInput}
          value={value}
          {Lit.refValue inputRef}
          @focus={fun _ ->
            inputRef.value |> Option.iter (fun el -> el.select())}
          @keyup={fun (ev: Event) ->
            ev.target.Value |> setValue}>
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
      <div style={Feliz.styles Styles.verticalContainer}>

        {buttonLit model dispatch}
        {if model.ShowReact then ReactLitComponent model.ShowClock else Lit.nothing}

        <!--{buttonFeliz model dispatch}-->
        <my-clock hourColor="red"></my-clock>

        {nameInput model.Value (ChangeValue >> dispatch)}
        {NameInput()}
      </div>
    """
    //   {itemList model}

Clock.register()

Program.mkProgram initialState update view
|> Program.withLit "app-container"
|> Program.run