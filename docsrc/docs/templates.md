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

### nothing

A sentinel value that signals a ChildPart to fully clear its content. Use this value when you don't want to render anything with Lit

```fsharp
html $"""Value: { if value > 0 then value else Lit.nothing } """
```

### classes

Generates a single string that filters out false-y values from a tuple sequence or a string sequence

```fsharp
let classes = ["button", true; "is-active", isActive] |> Seq.ofList |> Lit.classes

html $"""<button class={classes}>Click me</button>"""
```

```fsharp
let classes =
    seq {
        for entry in entries do
            $"is-{entry.kind}"
    }

html $"""<div class={classes}>... some content ...</div>"""
```

### memoize

If your templates are expensive to calculate you can use memoize (cache)

```fsharp
let recalculate =
    //... do the thing!
    setState result
let getExpensveTemplate value = // ...do the other thing!
html
    $"""
    <div>
        <button @click={recalculate}>Re-calculate</button>
        {Lit.memoize(getExpensiveTemplate state)}
    </div>
    """
```

### ofSeq

Merge several items in a single template result

```fsharp
let getItemTemplate item =
    html $"""<li>{item.id} - {item.name}</li>"""

html
    $"""
    <ul>
        {items |> Seq.map getItemTemplate |> Lit.ofSeq)}
    </ul>
    """
```

### ofList

Merge several items from a list in a single template result

```fsharp
let getItemTemplate item =
    html $"""<li>{item.id} - {item.name}</li>"""

html
    $"""
    <ul>
        {items |> List.map getItemTemplate |> Lit.ofList)}
    </ul>
    """
```

### mapUnique

Give a unique id to items in a list. This can improve performance in lists that will be sorted, filtered or re-ordered.

```fsharp
let getItemTemplate item =
    html $"""<li>{item.name} - {item.price}</li>"""

html
    $"""
    <ul>
        {Lit.mapUnique
            (fun item -> item.id)
            getItemTemplate
            state.items
        }
    </ul>
    """
```

### ofLazy

Prevents re-render of a template function until a single value or an array of values changes.

```fsharp
let name, setState = Hook.useState "Peter"
let age, setAge = Hook.useState 10

let nameTemplate _ =
    $"Hey {name}! "
let ageTemplate _ =
    $"you are {age} years old!"

html
    $"""
    <section>
        <input type="text" @input={EvVal setName} />
    </section
    <section>
        <input type="number" @input={EvVal setAge} />
    </section
    <div>
    {Lit.ofLazy [name] nameTemplate}{Lit.ofLazy [age] ageTemplate}
    </div>
    """
```

### ofPromise

Shows the placeholder until the promise is resolved

```fsharp
let loadingTpl =
    $"<div>Loading...</div>"

let startLoading =
    promise {
        do! Promise.sleep(2000)
        let template =
            // ... build a template from an async resource
        return template
    }

html
    $"""
    <div>
        {Lit.ofPromise loadingTpl startLoading}
    </div>
    """
```

### ofStr | ofText

Convert a single string into a TemplateResult

```fsharp
let value = "my-value"
html
    $"""
    <div>
        {Lit.ofStr value}
    </div>
    """
```

### ofInt

Convert a int into a TemplateResult

```fsharp
let value = 10
html
    $"""
    <div>
        The value is {Lit.ofInt value}
    </div>
    """
```

### ofFloat

Convert a float into a TemplateResult

```fsharp
let x, y = 120.5, 202.5
html
    $"""
    <div>
        The point is at {Lit.ofFloat x}, {Lit.ofFloat y}
    </div>
    """
```

### attrOfOption

Sets the attribute if the value is defined and removes the attribute if the value is undefined.

```fsharp

let color =
    match state.color with
    | "red"-> Some state.color
    | "#FF0000" -> Some state.color
    | _ -> None


html
    $"""
    <div color={Lit.attrOption color}>
        placeholder
    </div>
    """
```

:::info
You can access to the raw bindings via the `LitBindings` static class.
Check the full list in the [lit docs](https://lit.dev/docs/api/directives/)
:::

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
