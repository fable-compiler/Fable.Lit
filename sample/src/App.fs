module App

open System
open Elmish
open Elmish.Lit
open Fable.Core
open Feliz
open Browser
open Browser.Types
open Helpers
open Lit

type Model =
    { CurrentTime: DateTime
      Value: string }

type Messages =
    | Tick of DateTime
    | ChangeValue of string

let initialState() =
    { CurrentTime = DateTime.Now
      Value = "World" }, Cmd.none

let update msg model =
    match msg with
    | Tick next -> { model with CurrentTime = next }, Cmd.none
    | ChangeValue newValue -> { model with Value = newValue }, Cmd.none

let timerTick dispatch =
    window.setInterval(fun _ ->
        dispatch (Tick DateTime.Now)
    , 1000) |> ignore

let clockHand (time: Time) =
    let length = time.Length
    let angle = 2.0 * Math.PI * time.ClockPercentage
    let handX = (50.0 + length * cos (angle - Math.PI / 2.0))
    let handY = (50.0 + length * sin (angle - Math.PI / 2.0))
    svg $"""
<line
  x1="50"
  y1="50"
  x2={handX}
  y2={handY}
  stroke={time.Stroke}
  stroke-width={time.StrokeWidth}>
</line>   
"""

let handTop (time: Time) =
    let length = time.Length
    let revolution = float time.Value
    let angle = 2.0 * Math.PI * (revolution / time.FullRound)
    let handX = (50.0 + length * cos (angle - Math.PI / 2.0))
    let handY = (50.0 + length * sin (angle - Math.PI / 2.0))
    svg $"""
<circle
  cx={handX}
  cy={handY}
  r="2"
  fill={time.Stroke}>
</circle>   
"""

let clock (time: DateTime) =
    html $"""
<svg viewBox="0 0 100 100"
     width="350px">
  <circle
    cx="50"
    cy="50"
    r="45"
    fill="#0B79CE"></circle>

  {clockHand time.AsHour}
  {handTop time.AsHour}

  {clockHand time.AsMinute}
  {handTop time.AsMinute}

  {clockHand time.AsSecond}
  {handTop time.AsSecond}

  <circle
    cx="50"
    cy="50"
    r="3"
    fill="#0B79CE"
    stroke="#023963"
    stroke-width="1">  
  </circle>   
</svg>
"""

let nameInput value dispatch =
    let containerCss = [
      Css.marginLeft (length.rem 2)
      Css.displayFlex
      Css.justifyContentSpaceAround
      Css.alignItemsFlexStart
      Css.flexDirectionColumn
    ]

    let inputCss = [
      Css.padding (length.rem 0.25)
      Css.fontSize (length.px 16)
      Css.width (length.px 250)
      Css.marginBottom (length.rem 1)
    ]

    html $"""
<div style={styles containerCss}>
  <input
    style={styles inputCss}
    value={value}
    @keyup={fun (ev: Event) ->
      ev.target.Value |> dispatch}>

  <span>Hello {value}!</span>
</div>
"""

let itemList model =
    let renderNumber (value: int) = 
        html $"""<li>Value: <strong>{value}</strong></li>"""

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
      <div class={classes ["content", true; "px-4", true; "has-text-primary", model.CurrentTime.Second % 2 = 0]}>
        <p>No Key Item List</p>
        <ul>{items |> List.map renderNumber}</ul>
        <p>Keyed Item List</p>
        <ul>{items |> repeat string renderNumber}</ul>
      </div>
    """

let view model dispatch =    
    html $"""
      {clock model.CurrentTime}
      {nameInput model.Value (ChangeValue >> dispatch)}
      {itemList model}
    """

Program.mkProgram initialState update view
|> Program.withSubscription (fun _ -> Cmd.ofSub timerTick)
|> Program.withLit "app-container"
|> Program.run