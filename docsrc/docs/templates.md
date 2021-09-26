---
title: Templates
layout: nacara-standard
---

Lit allows you to render web UIs in a declarative way as [React](https://reactjs.org/) does by using HTML templates. With Fable.Lit you can use [F# interpolated strings](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/interpolated-strings) to build the HTML templates. Also, if you're using Visual Studio Code, we recommend the [Highlight F# Templates](https://marketplace.visualstudio.com/items?itemName=alfonsogarciacaro.vscode-template-fsharp-highlight) extension for IDE support of HTML and CSS as embedded languages in F#.

If you check [Lit's website](https://lit.dev) you will likely think it focus on [Web Components](https://developer.mozilla.org/en-US/docs/Web/Web_Components) (which [are also part of](./web-components.html) Fable.Lit), but HTML templates by themselves are already a very powerful feature and it's entirely possible to build your web app with them, or even use "virtual" [React-like components](./hook-components.html).

You can use the holes in the interpolated string to add dynamic data to the HTML templates. Normally the holes are only allowed in the attribute value or child node position.

When you open the `Lit` namespace, besides the `html` and `svg` helpers to declare the templates, you will have access to the `Lit` class that provides the following directives as static members:

TODO: example

Lit uses standard HTML for the templates, with only three special characters `@`/`?`/`.` in some situations:

<table>
    <thead>
        <tr>
            <th>Type</th>
            <th>Example</th>
        </tr>
    </thead>
    <tbody>
        <tr>
            <td>Event listeners</td>
            <td>

```fsharp
html $"""<button @click={Ev(fun ev -> doSomething())}>Click me!</button>"""
```

</td>
        </tr>
        <tr>
            <td>Boolean attributes</td>
            <td>

```fsharp
html $"""<button ?disabled={not enabled}>Click me!</button>"""
```

</td>
        </tr>
        <tr>
            <td>Properties</td>
            <td>

```fsharp
html $"""<my-component .someData={nonStringData}></my-component>"""
```

</td>
        </tr>
    </tbody>
</table>

:::info
Given that holes in interpolated strings are not typed, is a good idea to wrap your event handler with `Ev` to make sure you're using the correct type. `EvVal` is also available to access `ev.target.value` string in common input's `change` events.
:::

:::info
You rarely need to pass a [property instead of an attribute](https://stackoverflow.com/a/6004028) unless you're using a Web Component that asks you to do so.
:::

If you want to learn more about templates, please check [Lit's website](https://lit.dev/docs/templates/overview/).

## Directives

Lit includes special functions, called "directives" that can control the way the templates are rendered. The `Lit` class provides the following directives as static members:

:::info
The name and signature of some directives have been adapted from Lit to be more idiomatic in F#.
:::

TODO


## Styling

TODO

## Elmish

Fable.Lit.Elmish allows you to write a frontend app using the popular [Elmish](https://elmish.github.io/) library by [Eugene Tolmachev](https://github.com/et1975) with a view function returning `Lit.TemplateResult`. The package also includes support for Webpack's [Hot Module Replacement](https://webpack.js.org/concepts/hot-module-replacement/) out-of-the-box thanks to [Maxime Mangel](https://twitter.com/MangelMaxime) original work with Elmish.HMR.

```fsharp
open Elmish
open Lit

type Model = ..
type Msg = ..

let init() = ..
let update msg model = ..
let view model dispatch = ..

open Lit.Elmish
open Lit.Elmish.HMR

Program.mkProgram initialState update view
|> Program.withLit "app-container"
|> Program.run
```
