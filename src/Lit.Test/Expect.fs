namespace Expect

open Fable.Core

module private ExpectUtil =
    [<Emit("throw $0", isStatement=true)>]
    let throw (error: obj): 'T = jsNative

    let quote (v: obj) =
        match v with
        | :? string as s -> box("\"" + s + "\"")
        | _ -> v

open ExpectUtil

// Because of performance issues, custom exceptions in Fable
// don't inherit JS Error. So we create our own binding here.
[<Global("Error")>]
type JsError(message: string) =
    class end

[<AttachMembers>]
type AssertionError<'T>(assertion: string, ?description: string, ?actual: 'T, ?expected: 'T, ?brief: bool) =
    inherit JsError(
        let brief = defaultArg brief false
        [
            "Expected " |> Some
            description |> Option.map (fun v -> $"'{v}' ")
            if not brief then actual |> Option.map (fun v -> $"{quote v} ")
            $"to {assertion} " |> Some
            if not brief then expected |> Option.map (fun v -> $"{quote v} ")
        ] |> List.choose id |> String.concat ""
    )
    // Test runner requires these properties to be settable, not sure why
    member val actual = actual with get, set
    member val expected = expected with get, set

type AssertionError =
    static member Throw(assertion: string, ?description, ?actual: 'T, ?expected: 'T, ?brief: bool) =
        AssertionError(assertion, ?description=description, ?actual=actual, ?expected=expected, ?brief=brief) |> throw

// TODO: String and collection assertions
[<RequireQualifiedAccess>]
module Expect =
    let equal expected actual =
        if not(actual = expected) then
            AssertionError.Throw("equal", actual=actual, expected=expected)

    let notEqual expected actual =
        if not(actual <> expected) then
            AssertionError.Throw("not equal", actual=actual, expected=expected)

    let greaterThan expected actual =
        if not(actual > expected) then
            AssertionError.Throw("be greater than", actual=actual, expected=expected)

    let lessThan expected actual =
        if not(actual < expected) then
            AssertionError.Throw("be less than", actual=actual, expected=expected)

    let greaterOrEqual expected actual =
        if not(actual >= expected) then
            AssertionError.Throw("be greater than or equal to", actual=actual, expected=expected)

    let lessOrEqual expected actual =
        if not(actual <= expected) then
            AssertionError.Throw("be less than or equal to", actual=actual, expected=expected)

    let betweenInclusive lowerBound upperBound actual =
        if not(lowerBound <= actual && actual <= upperBound) then
            AssertionError.Throw($"be between inclusive {lowerBound} and {upperBound}", actual=actual)

    let betweenExclusive lowerBound upperBound actual =
        if not(lowerBound < actual && actual < upperBound) then
            AssertionError.Throw($"be between exclusive {lowerBound} and {upperBound}", actual=actual)

    let isTrue (msg: string) (condition: 'T -> bool) (actual: 'T) =
        if not(condition actual) then
            AssertionError.Throw("be true", description=msg)

    let isFalse (msg: string) (condition: 'T -> bool) (actual: 'T) =
        if condition actual then
            AssertionError.Throw("be false", description=msg)

    let find (msg: string) (condition: 'T -> bool) (items: 'T seq) =
        items
        |> Seq.tryFind condition
        |> function
            | Some x -> x
            | None -> AssertionError.Throw("be found", description=msg)

    let error (msg: string) (f: 'T -> 'Result) (actual: 'T) =
        try
            let _ = f actual
            AssertionError(msg, actual=actual) |> throw
        with e -> e

    let beforeTimeout (ms: int) (msg: string) (pr: JS.Promise<'T>): JS.Promise<'T> =
        JS.Constructors.Promise.race [|
            pr |> Promise.map box
            Promise.sleep ms |> Promise.map (fun _ -> box "timeout")
        |]
        |> Promise.map (function
            | :? string as s when s = "timeout" ->
                AssertionError.Throw($"happen before {ms}ms timeout", description=msg)
            | v -> v :?> 'T)
