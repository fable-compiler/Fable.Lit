module Clock

open System
open Browser
open Elmish
open Lit
open Helpers

type Model =
    { CurrentTime: DateTime }

type Msg =
    | Tick of DateTime

let init(v) =
    { CurrentTime = DateTime.Now }, Cmd.none

let update msg model =
    match msg with
    | Tick next -> { model with CurrentTime = next }, Cmd.none

let subscribe dispatch =
    let dispatch _ = Tick DateTime.Now |> dispatch
    window.setInterval(dispatch, 1000) |> ignore

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

let view model _dispatch =
    let time = model.CurrentTime
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
