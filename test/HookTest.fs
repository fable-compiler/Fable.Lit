module HookTest

open Elmish
open Lit
open Expect
open Expect.Dom
open WebTestRunner

[<HookComponent>]
let Counter () =
    let value, setValue = Hook.useState 5

    html
        $"""
      <div>
        <!-- Check snapshot can contain # char. See https://github.com/modernweb-dev/web/issues/1690 -->
        <p>F# counter</p>
        <p>Value: {value}</p>
        <button @click={Ev(fun _ -> value + 1 |> setValue)}>Increment</button>
        <button @click={Ev(fun _ -> value - 1 |> setValue)}>Decrement</button>
      </div>
    """

[<HookComponent>]
let Disposable (r: ref<int>) =
    let value, setValue = Hook.useState 5

    Hook.useEffectOnce
        (fun () ->
            r := r.Value + 1
            Hook.createDisposable (fun () -> r := r.Value + 10))

    html
        $"""
      <div>
        <p>Value: {value}</p>
        <button @click={Ev(fun _ -> value + 1 |> setValue)}>Increment</button>
        <button @click={Ev(fun _ -> value - 1 |> setValue)}>Decrement</button>
      </div>
    """

[<HookComponent>]
let DisposableContainer (r: ref<int>) =
    let disposed, setDisposed = Hook.useState false

    html
        $"""
      <div>
        <button @click={Ev(fun _ -> setDisposed true)}>Dispose!</button>
        {if not disposed then
             Disposable(r)
         else
             Lit.nothing}
      </div>
    """

[<HookComponent>]
let MemoizedValue () =
    let state, setState = Hook.useState (10)
    let second, setSecond = Hook.useState (0)
    let value = Hook.useMemo (fun _ -> second + 1)

    html
        $"""
        <div>
            <p id="state">{state}</p>
            <p id="second">{second}</p>
            <p id="memoized">{value}<p>
            <button @click={Ev(fun _ -> setState (state + 1))}>State</button>
            <button @click={Ev(fun _ -> setSecond (second + 1))}>Second</button>
        </div>
        """

type private State = { counter: int; reset: bool }

type private Msg =
    | Increment
    | Decrement
    | DelayedReset
    | AfterDelay

let private init () = { counter = 0; reset = false }, Cmd.none

let private update msg state =
    match msg with
    | Increment ->
        { state with
              counter = state.counter + 1 },
        Cmd.none
    | Decrement ->
        { state with
              counter = state.counter - 1 },
        Cmd.none
    | DelayedReset -> state, Cmd.OfAsync.perform (fun (t: int) -> Async.Sleep t) 200 (fun _ -> AfterDelay)
    | AfterDelay -> { state with counter = 0; reset = true }, Cmd.none

[<HookComponent>]
let ElmishComponent () =
    let state, dispatch = Hook.useElmish (init, update)

    let resetDisplay() =
        if state.reset then html $"""<p id="reset">Has reset!</p>"""
        else Lit.nothing

    html
        $"""
            <div>
               <p id="count">{state.counter}</p>
               {resetDisplay()}
               <button @click={Ev(fun _ -> dispatch Increment)}>Increment</button>
               <button @click={Ev(fun _ -> dispatch Decrement)}>Decrement</button>
               <button @click={Ev(fun _ -> dispatch DelayedReset)}>Delayed Reset</button>
            </div>
          """

describe "Hook" <| fun () ->
    it "counter renders" <| fun () -> promise {
        use! el = Counter() |> render
        return! el.El |> Expect.matchHtmlSnapshot "counter"
    }

    it "increases/decreases the counter on button click" <| fun () -> promise {
        use! el = Counter() |> render
        let el = el.El
        let valuePar = el.getByText("value")
        let incrButton = el.getButton("increment")
        let decrButton = el.getButton("decrement")
        incrButton.click()
        valuePar |> Expect.innerText "Value: 6"
        decrButton.click()
        decrButton.click()
        valuePar |> Expect.innerText "Value: 4"
    }

    it "useEffectOnce runs on mount/dismount" <| fun () -> promise {
        let aRef = ref 8
        use! el = DisposableContainer(aRef) |> render
        let el = el.El
        el.getByText("value") |> Expect.innerText "Value: 5"

        // Effect is run asynchronously after after render
        do! Promise.sleep 100
        aRef.Value |> Expect.equal 9

        // Effect is not run again on rerenders
        el.getButton("increment").click()
        el.getByText("value") |> Expect.innerText "Value: 6"
        aRef.Value |> Expect.equal 9

        // Cause the component to be dismounted
        el.getButton("dispose").click()
        // Effect has been disposed

        aRef.Value |> Expect.equal 19
    }

    it "useMemo doesn't change without dependencies" <| fun () -> promise {
        use! el = MemoizedValue() |> render
        let el = el.El
        let state = el.getButton("state")
        let second = el.getButton("second")

        state.click()
        el.querySelector("#state") |> Expect.innerText "11"
        // un-related re-renders shouldn't affect memoized value
        el.querySelector("#memoized") |> Expect.innerText "1"
        // change second value trice
        second.click()
        second.click()
        second.click()

        // second should have changed
        el.querySelector("#second") |> Expect.innerText "3"
        // memoized value should have not changed
        el.querySelector("#memoized") |> Expect.innerText "1"
    }

    it "useElmish dispatches messages correctly" <| fun () -> promise {
        use! el = ElmishComponent() |> render
        let el = el.El
        let inc = el.getButton("increment")
        let decr = el.getButton("decrement")

        let delayedReset = el.getButton("delay")
        // normal dispatch works
        el.querySelector("#count") |> Expect.innerText "0"
        inc.click()
        inc.click()

        // normal dispatch works
        el.querySelector("#count") |> Expect.innerText "2"
        decr.click()
        decr.click()

        el.querySelector("#count") |> Expect.innerText "0"
        decr.click()
        decr.click()

        // normal dispatch works
        el.querySelector("#count") |> Expect.innerText "-2"

        delayedReset.click()
        el.querySelector("#count") |> Expect.innerText "-2"
        let! _reset =
            Expect.retryUntil "reset text appears" (fun () ->
                el.getSelector("#reset"))
        // dispatch with async cmd works
        el.querySelector("#count") |> Expect.innerText "0"
    }