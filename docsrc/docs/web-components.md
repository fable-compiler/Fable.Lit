---
title: Web Components
layout: nacara-standard
---

[web components]: https://developer.mozilla.org/en-US/docs/Web/Web_Components
[fast]: https://fast.design
[spectrum web components]: https://github.com/adobe/spectrum-web-components
[vaadin]: https://vaadin.com/components
[fluent ui]: https://github.com/microsoft/fluentui/tree/master/packages/web-components
[lit mechanisms to declare reactive properties]: https://lit.dev/docs/components/properties/
[ionic framework]: https://ionicframework.com/docs/components
[hooks]: ./hook-components.html
[testing]: ./testing.html
[custom css properties]: https://developer.mozilla.org/en-US/docs/Web/CSS/Using_CSS_custom_properties
[css parts]: https://developer.mozilla.org/en-US/docs/Web/CSS/::part
[shadow dom]: https://developer.mozilla.org/en-US/docs/Web/Web_Components/Using_shadow_DOM

[Web Components] are a way to create custom reusable HTML Elements and are part of the browser standards. These elements work as any other HTML tag like a `div`, `span`, or `article` meaning that you can produce web components that are able to be used by any framework without having to write any kind of wrapper on top of it.

> These web components use [Lit](https://lit.dev) under the hood, visit [lit.dev](https://lit.dev) for more detailed information on how `LitElement` Web Components work.

Let's see a quick simple example of a web component

```fsharp
[<LitElement("my-counter")>]
let Counter() =
    // This call is obligatory to initialize the web component
    let _, props =
        LitElement.init(
            fun init ->
                let props = {| initial = Prop.of(0, attribute = "initial") |}
                init.props <- props)

    let counter, setCounter = Hook.useState props.initial.Value

    html
        $"""
        <article>
            <p>{counter}</p>
            <button @click={fun _ -> setCounter(counter + 1)}>+</button>
            <button @click={fun _ -> setCounter(counter - 1)}>-</button>
        </article>
        """
```

:::info
Check [hooks] for a more detailed explanation of how to use them.
:::

And then in HTML you can use it as

```html
<my-counter></my-counter>
```

```html
<my-counter initial="10"></my-counter>
```

This will produce two different instances of `my-counter` the first one will start at 0 the second one will start at 10

> When we say you can use it in HTML we mean it, wherever you are used to put an HTML tag you are able to use it, vue, angular, server side pages... You name it.

To be able to define web components with Lit (and Fable.Lit) you need to pay attention to the following things since we have abstracted some of the [Lit mechanisms to declare reactive properties] to have a nicer API from F#'s perspective

<table>
    <thead>
        <th>Name</th>
        <th>Js Counterpart</th>
    </thead>
    <tbody>
        <tr>
            <td>

```fsharp
[<LitElement("element-name")>]
let ConstructorFunction() =
```

</td>
            <td>

```js
customElements.define("element-name", ConstructorFunction);
```

</td>
        </tr>
        <tr>
            <td>

```fsharp
let host, properties = LitElement.init(initFn)
```

</td>
            <td>

```js
Component.properties = function () {
  return properties;
};
Component.styles = function () {
  return styles;
};
```

</td>
        </tr>
        <tr>
            <td>

```fsharp
{| property = Prop.Of("defaultValue") |}
```

</td>
            <td>

```js
constructor() {
    this.property = "defaultValue"
}
```

</td>
        </tr>
    </tbody>
</table>

## Properties

When you call `let host, properties = LitElement.init initFunction` you are initializing a web component instance since web components are not "constructible" (i.e. you can't do `new MyElement()`, you have to do `document.createElement('my-element')`) there's not a direct way to pass external information for that you have two ways to do it

- properties
  ```js
  const el = document.querySelector("my-element");
  el.property = "new value";
  ```
- attributes
  ```html
  <my-element my-attribute="attribute-value"></my-element>
  ```

Properties are existing members of an instance while attributes are strings defined in HTML in Fable.Lit you can use the `Prop.Of` API to get your properties in place

```fsharp
let initFn init =
    init.props <- {| name = Prop.Of("", attribute = "name"); age = Prop.Of(10, attribute = "age")  |}
[<LitElement("my-component")>]
let MyComponent() =
    let _, props = LitElement.init initFn
    (* ...  the rest of the function ... *)
```

```fsharp
Prop.Of(
    "defaultValue",
    attribute = "name-of-the-attribute",
    hasChanged = boolPredicateFn,
    fromAttribute = stringToTypeFunctionFn,
    toAttribute = typeToStringFunctionFn,
    reflect = false
)
```

:::info
When you use this API each of the properties declared wil be accessible via the element's instance, except from the first parameter all of the other are optional
:::

:::warning
Use attribute for primitive values only, otherwise Json objects and Arrays will be serialized using `JSON.(parse|stringify)`, for complex objects use properties only to prevent attribute strings converting your object in a string
:::

## Events

With properties we can obtain information from the outside world, but to let the outside world something happened we use events. Events while not strictly required, they should be triggered by a user interaction a click, a selection a scroll rather than emitted when a property changes.

```fsharp
[<LitElement("product-settings")>]
let CloseMe() =
    let host, props =
        LitElement.init(fun init ->
            init.props <- {| product = Prop.Of({ (* ... product definition ...*) }) |})

    let product = props.product.Value

    let onDeleteFromInventory _ =
        host.dispatchEvent("on-delete-from-inventory")

    let onHideFromCustomers _ =
        host.dispatchCustomEvent("on-hide-from-customers", { product with hidden = true })

    html
        $"""
        <article>
            <header>
                <h3>{product.name}</h3>
            </header>
            <aside>
                <button @click={onHideFromCustomers}>Delete</button>
                <button @click={onDeleteFromInventory}>Hide</button>
            </aside>
            <section>
                <!-- the rest of the component -->
            </section>
        </article>
        """
```

`host.dispatchEvent` and `host.dispatchCustomEvent` have the following optional arguments

- detail -> (CustomEvent only) A value or object that you want to send to the listeners of this event
- bubbles -> Allow the event to enter the bubblibg phase
- composed -> Let the event cross the shadow DOM boundary
- cancelable -> Allow this event to be default prevented

:::info
These parameters are set to true by default (except from detail which is None by default) for convenience since most of the time this is what you want when you dispatch events
:::

## Styles

Web components internals live in what is called [Shadow DOM] this means the normal document styles don't affect them as an example doing the following in the head of the document

```css
p {
  color: red;
}
```

would paint every `p` element in the document except those inside the shadow DOM.

To define styles for your web components you have to provide them in the init function of the component.

```fsharp
[<LitElement("my-element")>]
let MyElement()
    let _ = LitElement.init(fun init ->
        init.styles <- [
            css $"""
                :host {{
                    display: flex;
                    flex-direction: column;
                }}

                p {{
                    color: red;
                }}
                """
            yield! Shared.styles
        ])
    html
        $"""
        <p>This text is red!</p>
        <my-other-element header="This element's p's won't be red"></my-other-element>
        """
```

Assuming `<my-other-element header="This element's p's won't be red"></my-other-element>` is another web component (meaning it has it's own Shadow DOM) the `p` tags inside that element won't be affected by our own CSS styles.

We have CSS Encapsulation but customization is possible via

- [Custom CSS Properties]
- [Css Parts]

A brief example of this would be defining the font color for a component

Having a web component defined like this

```fsharp
[<LitElement("my-element")>]
let MyElement()
    let _ = LitElement.init(fun init ->
        init.styles <- [
            css $"""
                p {{
                    color: var(--primary-color);
                }}
                button {{
                    color: pink;
                }}
                """
        ])
    html
        $"""
        <p>This text is red!</p>
        <button part="hi-btn">Say Hi</button>
        """
```

we can define a CSS stylesheet in the normal document, either a `link` tag or a `style` tag in the head of the document works.

```css
:root {
  --primary-color: rebeccapurple;
}

.primary-red {
  --primary-color: red;
}

.primary-blue {
  --primary-blue: blue;
}

.primary-blue my-element::part(hi-btn) {
  color: blue;
  border: 2px solid pink;
}
```

Here we are going to leverage the cascading aspect of the CSS the `--primary-color` variable has a rule to match from the `:root` of the document, then each time we're inside a particular class we're going to override that variable to give it a new value, our web component will consume the final value of that variable `var(--primary-color)`, in the case of the part, we defined the rule when the element is a child of `.primary-blue` we will style the `part` completely at our own taste

```html
<section>
  <!-- color: rebeccapurple -->
  <my-element></my-element>
</section>
<section class="primary-red">
  <!-- color: red -->
  <my-element></my-element>
</section>
<section class="primary-blue">
  <!-- color: blue -->
  <!-- button border: pink -->
  <my-element></my-element>
</section>
```

between parts and variables you can expose only those particular areas of your components that you as an author are allowing to modify

:::info
To know more about applying styles to LitElement check the [theming](https://lit.dev/docs/components/styles/#theming) docs in their site.
:::

## Use cases

Popular libraries made with Web components are

- [FAST] - Microsoft
  - [Fluent UI] - The design langauge that powers Windows
- [Spectrum Web Components] - Adobe Spectrum design language
- [Vaadin]
- [Ionic Framework]

> Most of those are actually build with Lit as well!

If these libraries already provide UI libraries why would you want to author web components?

- Testability
- Design Systems/Branding
- Portability

#### Testability

Components are a single unit of the UI they have behavior and state it's easier to test them in isolation.
They are similar to functions

- `inputs -> processing -> outputs`
- `attr/props -> processing | User Interaction -> UIChanges | Events`

That means that it should be easy to determine which things changed inside a component when their properties or attributes have changed, if the user changed something or if the element is trying to communicate with the parent elements

:::info
Check [Testing] to know more about how can you test Lit components
:::

#### Design Systems

If you're part of a company with multiple teams, chances are that those teams might have chosen different tools to work with if your company wants to implement a branding or a uniform way to distinguish itself from others it would be hard to create components in each library, web components are a perfect fit for that, Adobe Spectrum and Ionic Framework are excelent examples of that they define dozens of web components with a particular design language that makes the brand easily recognizable.

#### Portability

Web components are designed to work in the browser, once you include the script tag and register them you can use them in any UI library Angular, Vue, Server, etc. write once use everywhere.
