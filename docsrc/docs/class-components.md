# Class Lit Components

Lit's own API is based on class components, not functions while Fable.Lit tries to provide the best experience for F# with idiomatic style you are very likely to know and love, we also understand that there's also things that are hard to do with the current API specially those that expect Lit Elements as classes.

The simplest component example would be the following:

```fsharp
type MyElement() =
    inherit LitElement()

    let mutable counter = 0

    let increment _ =
        counter <- counter + 1

    let decrement _ =
        counter <- counter + 1

    override _.render() =
        html
            $"""
            <p>{counter}</p>
            <button @click={increment}>Increment</button>
            <button @click={decrement}>Decrement</button>
            """
// runs the auto-registration process for this custom element
// similar to the @customElement("name") decorator from Lit
registerElement<MyElement> "element-with-controller" {
    // registers a property to be tracked by lit
    reactiveProperty "counter" {
        use_state
    }

    css $"p {{ color: red; }}"
}
```

As you can see this is pretty much how you would write a custom element with typescript

```ts
@cutomElement("my-element")
class MyElement extends LitElement {
  static styles = [
    css`
      p {
        color: red;
      }
    `,
  ];

  @state
  private counter = 0;

  private increment = () => {
    this.counter += 1;
  };

  private decrement = () => {
    this.counter -= 1;
  };

  render() {
    return html`
      <p>${this.counter}</p>
      <button @click=${increment}>Increment</button>
      <button @click=${decrement}>Decrement</button>
    `;
  }
}
```

### Main Differences

One difference you can see with the function based API is that the configuration is not done via the init function, which has advantages and disadvantages.

- Function components with the `ComponentFunction` and `LitElement` attributes are compatible with HMR
- Classes are as close as lit's API as you will get
- Functions use hook style composition
- Classes use controller style composition

### When to use class components

Class components lend themselves to be used more with more mutable state management and also the usage of reactive controllers which may come from third party packages or even lit's official packages. Consider using class components when:

- You are already familiar with Lit and come from typescript.
- Your component uses several controllers or has complex mutable state.
- You want to stay as close to Lit as possible.

### When not to use class components

Consider function components when:

- You are familiar with the Feliz/Fable Ecosystem
- You are getting started with Lit and F#
- You are familiar with React
