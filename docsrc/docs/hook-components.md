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

> Note that hook components are just a way to keep state between renders and are not [web components](https://www.webcomponents.org/introduction). We plan to add bindings to define web components with [lit](https://lit.dev) in the near future. Also check [Fable.Haunted](https://github.com/AngelMunoz/Fable.Haunted) by Angel Munoz to define actual web components with React-style hooks.


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
