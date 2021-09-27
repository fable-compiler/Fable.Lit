---
title: Hook Components
layout: nacara-standard
---

Fable.Lit includes the `HookComponent` attribute. When you decorate a view function with it, this lets you use [hooks](https://reactjs.org/docs/hooks-overview.html) in a similar way as [ReactComponent](https://zaid-ajaj.github.io/Feliz/#/Feliz/React/NotJustFunctions) attribute does. Hook support is included in Fable.Lit's F# code and doesn't require any extra JS dependency besides Lit.

```fsharp
open Lit

[<HookComponent>]
let NameInput() =
    let value, setValue = Hook.useState "World"
    let inputRef = Hook.useRef<HTMLInputElement>()

    html $"""
      <div class="content">
        <p>Hello {value}!</p>
        <input {Ref inputRef}
          value={value}
          @keyup={EvVal setValue}
          @focus={Ev(fun _ -> inputRef.Value |> Option.iter (fun el -> el.select()))}>
      </div>
    """
```

:::info
Note that HookComponents are just a way to keep state between renders and doesn't create a custom HTML element. Check [Web Components](./web-components.html) if you want to declare a component that can be instantiated from HTML.
:::

Fable.Lit hooks in general have the same API as their React counterparts but may differ in some occasions:

- `useState`
- `useMemo`
- `useRef`: Can use "native" F# refs.
- `useEffect`: Doesn't accept a dependency array, instead it provides semantic alternatives for each use case.
    - `useEffect`: Trigger an effect after each render.
    - `useEffectOnce`: Trigger an effect only once after the first render.
    - `useEffectOnChange`: Trigger an effect after each render **if** the given value has changed.

## UseElmish

Thanks to the great work by [Cody Johnson](https://twitter.com/Cody_S_Johnson) with [Feliz.UseElmish](https://zaid-ajaj.github.io/Feliz/#/Hooks/UseElmish), Fable.Lit HookComponents also include `useElmish` hook to manage the internal state of your components using the model-view-update architecture.

```fsharp
open Elmish
open Lit

type Model = ..
type Msg = ..

let init() = ..
let update msg model = ..
let view model dispatch = ..

[<HookComponent>]
let Clock(): TemplateResult =
    let model, dispatch = Hook.useElmish(init, update)
    view model dispatch
```

## Writing your own hook

The magic behind Fable.Lit's hooks is the context is provided by the JS `this` keyword. To access the context from the render function you must use an `inline` helper and then pass the context to your custom hook. Example:

```fsharp
module MyHooks =
    // Updating a ref doesn't cause a re-render,
    // so let's use a ref with the same signature as useState
    let useSilentState (ctx: HookContext, v: 'Value) =
        let r = ctx.useRef(v)
        r.Value, fun v -> r := v

    type Lit.Hook with
        // IMPORTANT! This function must be inlined to access the context from the render function
        static member inline useSilentState(v: 'Value): 'Value * ('Value -> unit) =
            useSilentState(Hook.getContext(), v)
```