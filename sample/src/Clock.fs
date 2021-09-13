module Clock

open System
open Fable.Core
open Browser.Types
open Elmish
open Lit
open Helpers

type Model =
    { CurrentTime: DateTime
      IntervalId: int
      MinuteHandColor: string }

    interface IDisposable with
        member this.Dispose() =
            JS.clearInterval this.IntervalId

    static member Empty =
        { CurrentTime = DateTime.Now
          IntervalId = 0
          MinuteHandColor = "white" }

type Msg =
    | Tick of DateTime
    | IntervalId of int
    | MinuteHandColor of string

let init() =
    let subscribe dispatch =
        JS.setInterval (fun () ->
            Tick DateTime.Now |> dispatch
        ) 1000
        |> IntervalId
        |> dispatch

    Model.Empty, Cmd.ofSub subscribe

let update msg model =
    match msg with
    | Tick next -> { model with CurrentTime = next }, Cmd.none
    | IntervalId id -> { model with IntervalId = id }, Cmd.none
    | MinuteHandColor value -> { model with MinuteHandColor = value }, Cmd.none

let handTop (color: string) (time: Time) =
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
          fill={color}>
        </circle>
    """

let clockHand (color: string) (time: Time) =
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
          stroke={color}
          stroke-width={time.StrokeWidth}>
        </line>
        {handTop color time}
    """

let colors = [
    "white"
    "red"
    "yellow"
    "purple"
]

let select options value dispatch =
    let option value =
        html $"""<option value={value}>{value}</option>"""

    html $"""
        <div class="select mb-2">
            <select value={value} @change={fun (ev: Event) -> ev.target.Value |> dispatch}>
                {options |> List.map option}
            </select>
        </div>
    """

let initEl (config: LitConfig<_>) =
    config.props <-
        {|
            hourColor = Prop.Of("lightgreen", attribute="hour-color")
        |}

    config.styles <- [
        css $"""
            .container {{
                display: flex;
                align-items: center;
                margin-bottom: .5em;
            }}
            p {{
                flex-grow: 1;
                margin: 0 10px;
                border-style: dotted;
                border-width: 4px;
                border-color: firebrick;
                color: deeppink;
                font-size: large;
                text-align: center;
            }}
        """
    ]

[<LitElement("my-clock")>]
let Clock() =
    let props = LitElement.init initEl
    let hourColor = props.hourColor.Value

    let model, dispatch = Hook.useElmish(init, update)
    let time = model.CurrentTime

    html $"""
        <svg viewBox="0 0 100 100"
             width="350px">
          <circle
            cx="50"
            cy="50"
            r="45"
            fill="#0B79CE"></circle>

          {clockHand hourColor time.AsHour}
          {clockHand model.MinuteHandColor time.AsMinute}
          {clockHand "#023963" time.AsSecond}

          <circle
            cx="50"
            cy="50"
            r="3"
            fill="#0B79CE"
            stroke="#023963"
            stroke-width="1">
          </circle>
        </svg>

        <div class="container">
            <p>This is a clock</p>
            {select colors model.MinuteHandColor (MinuteHandColor >> dispatch)}
        </div>
    """

// Make sure this file is being called by the app entry
let register() = ()