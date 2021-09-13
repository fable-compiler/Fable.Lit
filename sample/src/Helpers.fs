module Helpers

type Time =
    | Hour of int
    | Minute of int
    | Second of int
    member this.Value =
        match this with
        | Hour n -> n
        | Second n -> n
        | Minute n -> n

    member this.ClockPercentage =
        (float this.Value) / this.FullRound

    member this.StrokeWidth =
        match this with
        | Hour _ | Minute _ -> 2
        | Second _ -> 1

    member this.Length =
        match this with
        | Hour _ -> 25.
        | Minute _ -> 35.
        | Second _ -> 40.

    member this.FullRound =
        match this with
        | Hour _ -> 12.
        | Second _ | Minute _ -> 60.

type System.DateTime with
    member this.AsHour = Hour this.Hour
    member this.AsMinute = Minute this.Minute
    member this.AsSecond = Second this.Second

type Browser.Types.EventTarget with
    member this.Value = (this :?> Browser.Types.HTMLInputElement).value

let evTargetValue (ev: Browser.Types.Event) =
    ev.target.Value