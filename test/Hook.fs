module Hook

open Fable.Core
open Browser.Types

open Elmish
open Lit
open Lit.Elmish

let createRef (v: 'T) = ref v
let refValue (r: 'T ref) = r.Value

[<HookComponent>]
let Counter () =
    let value, setValue = Hook.useState 5

    html
        $"""
      <div>
        <p>Value: {value}</p>
        <button class="incr" @click={fun _ -> value + 1 |> setValue}>Increment</value>
        <button class="decr" @click={fun _ -> value - 1 |> setValue}>Decrement</value>
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
        <button class="incr" @click={fun _ -> value + 1 |> setValue}>Increment</value>
        <button class="decr" @click={fun _ -> value - 1 |> setValue}>Decrement</value>
      </div>
    """

[<HookComponent>]
let DisposableContainer (r: ref<int>) =
    let disposed, setDisposed = Hook.useState false

    html
        $"""
      <div>
        <button class="dispose" @click={fun _ -> setDisposed true}>Dispose!</button>
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
            <button id="state-btn" @click={fun _ -> setState (state + 1)}></button>
            <button id="second-btn" @click={fun _ -> setSecond (second + 1)}></button>
        </div>
        """

type private State = { counter: int }

type private Msg =
    | Increment
    | Decrement
    | DelayedReset
    | AfterDelay

let private init () = { counter = 0 }, Cmd.none

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
    | AfterDelay -> { state with counter = 0 }, Cmd.none

[<HookComponent>]
let ElmishComponent () =
    let state, dispatch = Hook.useElmish (init, update)

    html
        $"""
            <div>
               <p id="count">{state.counter}</p>
               <button id="inc" @click={fun _ -> dispatch Increment}></button>
               <button id="decr" @click={fun _ -> dispatch Decrement}></button>
               <button id="delay-reset" @click={fun _ -> dispatch DelayedReset}></button>
            </div>
          """
