module Hook

open Fable.Core
open Browser.Types
open Lit

[<HookComponent>]
let Counter() =
    let value, setValue = Hook.useState 5

    html $"""
      <div>
        <p>Value: {value}</p>
        <button class="incr" @click={fun _ -> value + 1 |> setValue}>Increment</value>
        <button class="decr" @click={fun _ -> value - 1 |> setValue}>Decrement</value>
      </div>
    """

[<HookComponent>]
let Disposable(r: RefValue<int>) =
    let value, setValue = Hook.useState 5
    Hook.useEffectOnce(fun () ->
        r.value <- r.value + 1
        Hook.createDisposable(fun () ->
            r.value <- r.value + 10
        )
    )

    html $"""
      <div>
        <p>Value: {value}</p>
        <button class="incr" @click={fun _ -> value + 1 |> setValue}>Increment</value>
        <button class="decr" @click={fun _ -> value - 1 |> setValue}>Decrement</value>
      </div>
    """
