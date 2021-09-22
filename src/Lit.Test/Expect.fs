namespace Expect

open Fable.Core

module private ExpectUtil =
    [<Emit("throw $0", isStatement=true)>]
    let throw (error: obj): 'T = jsNative

    let addTextLeft (str: string option) =
        match str with Some s -> s + " " | None -> ""

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
type AssertionError<'T>(assertion: string, actual: 'T, ?expected: 'T, ?prefix: string) =
    inherit JsError(
        $"""Expected {addTextLeft prefix}{quote actual} to {assertion} {quote expected}"""
    )
    // Test runner requires these properties to be settable, not sure why
    member val actual = actual with get, set
    member val expected = expected with get, set

type AssertionError =
    static member Throw(assertion: string, actual: 'T, ?expected: 'T, ?prefix: string) =
        AssertionError(assertion, actual, ?expected=expected, ?prefix=prefix) |> throw

// TODO: String and collection assertions
[<RequireQualifiedAccess>]
module Expect =
    let equal expected actual =
        if not(actual = expected) then
            AssertionError("equal", actual=actual, expected=expected) |> throw

    let notEqual expected actual =
        if not(actual <> expected) then
            AssertionError("not equal", actual=actual, expected=expected) |> throw

    let greaterThan expected actual =
        if not(actual > expected) then
            AssertionError("be greater than", actual=actual, expected=expected) |> throw

    let lessThan expected actual =
        if not(actual < expected) then
            AssertionError("be less than", actual=actual, expected=expected) |> throw

    let greaterOrEqual expected actual =
        if not(actual >= expected) then
            AssertionError("be greater than or equal to", actual=actual, expected=expected) |> throw

    let lessOrEqual expected actual =
        if not(actual <= expected) then
            AssertionError("be less than or equal to", actual=actual, expected=expected) |> throw

    let betweenInclusive lowerBound upperBound actual =
        if not(lowerBound <= actual && actual <= upperBound) then
            AssertionError($"be between inclusive {lowerBound} and {upperBound}", actual=actual) |> throw

    let betweenExclusive lowerBound upperBound actual =
        if not(lowerBound < actual && actual < upperBound) then
            AssertionError($"be between exclusive {lowerBound} and {upperBound}", actual=actual) |> throw

    let isTrue (msg: string) (condition: 'T -> bool) (actual: 'T) =
        if not(condition actual) then
            AssertionError(msg, actual=actual) |> throw

    let isFalse (msg: string) (condition: 'T -> bool) (actual: 'T) =
        if condition actual then
            AssertionError(msg, actual=actual) |> throw

    let error (msg: string) (f: 'T -> 'Result) (actual: 'T) =
        try
            let _ = f actual
            AssertionError(msg, actual=actual) |> throw
        with e -> e
