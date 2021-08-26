module App

open System
open Browser.Types
open Feliz
open type length
open Elmish
open Lit
open Helpers

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
    toLitStatic <| Html.button [
        Attr.className "button"
        Ev.onClick (fun _ -> not model.ShowClock |> ShowClock |> dispatch)

        // This doesn't work, template is only built once and cached
        // if model.ShowClock then
        //     Html.text "Hide clock"
        // else
        //     Html.strong "Show clock"

        // Do this instead
        ofLit <|
            if model.ShowClock then
                toLitStatic <| Html.text "Hide clock"
            else
                toLitStatic <| Html.strong "Show clock"
    ]

let buttonLit (model: Model) dispatch =
    html $"""
        <button class="button" @click={fun _ -> not model.ShowClock |> ShowClock |> dispatch}>
          {if model.ShowClock then "Hide clock" else "Show clock"}
        </button>
    """

let nameInput value dispatch =
    let containerCss = [
        Css.marginLeft(rem 2)
        Css.displayFlex
        Css.justifyContentCenter
        Css.alignItemsCenter
        Css.flexDirectionColumn
    ]

    let inputCss = [
      Css.padding(rem 0.25)
      Css.fontSize(px 16)
      Css.width(px 250)
      Css.marginBottom(rem 1)
    ]

    let inputRef = createRef<HTMLInputElement>()

    html $"""
      <div style={styles containerCss}>
        <input
          {refValue inputRef}
          style={styles inputCss}
          value={value}
          @focus={fun _ ->
            inputRef.value |> Option.iter (fun el -> el.select())}
          @keyup={fun (ev: Event) ->
            ev.target.Value |> dispatch}>

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

let view model dispatch =
    html $"""
      <div style={styles [
        Css.margin(rem 2)
        Css.displayFlex
        Css.justifyContentCenter
        Css.alignItemsCenter
        Css.flexDirectionColumn
      ]}>
        {buttonFeliz model dispatch}
        {if model.ShowClock
         then Clock.view model.Clock (ClockMsg >> dispatch)
         else nothing}
      </div>
      {nameInput model.Value (ChangeValue >> dispatch)}
    """
      // {itemList model}
      // {dummyInput() |> toLit}

Program.mkProgram initialState update view
|> Program.withSubscription (fun _ -> Cmd.ofSub(fun d -> Clock.subscribe (ClockMsg >> d)))
|> Program.withLit "app-container"
|> Program.run