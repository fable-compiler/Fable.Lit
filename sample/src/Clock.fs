module Clock

open System
open Fable.Core
open Browser
open Elmish
open Lit
open Helpers

type Model =
    { CurrentTime: DateTime
      IntervalId: int }
    interface IDisposable with
        member this.Dispose() =
            JS.clearInterval this.IntervalId

type Msg =
    | Tick of DateTime
    | IntervalId of int

let init() =
    let subscribe dispatch =
        JS.setInterval (fun () ->
            Tick DateTime.Now |> dispatch
        ) 1000
        |> IntervalId
        |> dispatch

    { CurrentTime = DateTime.Now
      IntervalId = 0 }, Cmd.ofSub subscribe

let update msg model =
    match msg with
    | Tick next -> { model with CurrentTime = next }, Cmd.none
    | IntervalId id -> { model with IntervalId = id }, Cmd.none

let clockHand (time: Time) =
    let length = time.Length
    let angle = 2.0 * Math.PI * time.ClockPercentage
    let handX = (50.0 + length * cos (angle - Math.PI / 2.0))
    let handY = (50.0 + length * sin (angle - Math.PI / 2.0))
    Lit.svg $"""
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
    Lit.svg $"""
        <circle
          cx={handX}
          cy={handY}
          r="2"
          fill={time.Stroke}>
        </circle>
    """

let view model _dispatch =
    let time = model.CurrentTime
    Lit.html $"""
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

[<HookComponent>]
let Clock() =
    let model, dispatch = Hook.useElmish(init, update)
    view model dispatch
