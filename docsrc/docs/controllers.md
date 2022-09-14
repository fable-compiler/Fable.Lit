# Controllers

> **Note**: To learn more about Reactive Controllers please visit the [lit's documentation](https://lit.dev/docs/composition/controllers/)

Lit Controllers are one way to apply composition to components, these are like React's Hooks but designed for lit.

Controllers allow you to tap into the component's life cycle that is using them which makes it easier to control when to render when a side effect has been produced and the component has to re-render.

Controllers are a class based API and while classes are not the favored construct in F#, F# knows quite well how to work with them while also providing means to ensure correctness.

A simple controller can be as follows

```fsharp
type Post =
    {| usedId: int
       id: int
       title: string
       body: string |}

type PostsController(host: LitElement) =
    inherit ReactiveController(host)

    // declare public properties
    // that can be accessed from other components
    // or used as view values
    member val Posts: Post array = Array.empty with get, set
    member val Page: int = 1 with get, set
    member val Limit: int = 10 with get, set

    // We can handle logig within the function or outsource it
    // to another function if required, in this case we'll just show it here
    // but it could have been something like
    // `member this.Fetch(?page: int, ?limit: int) = Server.fetchPosts page limit
    member this.Fetch(?page: int, ?limit: int) =
        let page = defaultArg page this.Page
        let limit = defaultArg limit this.Limit

        promise {
            let! posts =
                promise {
                    let! result = tryFetch $"https://jsonplaceholder.typicode.com/posts?_page={page}&_limit={limit}" []

                    match result with
                    | Ok res ->
                        let! result = res.json () :?> JS.Promise<Post array>
                        return result
                    | Error err -> return Array.empty<Post>
                }
            // do the assignments as usual
            this.Posts <- posts
            // tap into the host's update mechanism
            // and request an re-render
            host.requestUpdate ()
        }

    member this.NextPage() =
        this.Page <- this.Page + 1
        this.Fetch() |> Promise.start

    // tap into the hosts's lifecycle when the component is connected
    // to the DOM, in vue or other frameworks
    // this could be called something like `mounted`
    override this.hostConnected() : unit = this.Fetch() |> Promise.start
```

Once this controller is declared we can use it in other places like

For Functions:

```fsharp
let postTemplate (post: Post) =
    html $"<li>{post.title}</li>"

[<LitElement("my-element")>]
let MyElement() =
    let host =
        LitElement.init(fun init ->
            init.controllers <- {| api = Controller.of(fun host -> PostsController host) |}
        )
    let api = host.controlelrs.api.Value

    html
        $"""
        <p>Page: {api.Page}</p>
        <button @click={fun _ -> api.NextPage()}>Next Page</button>
        <ul>{posts}</li>
        """
// Alternatively
[<LitElement("my-element")>]
let MyElement() =
    LitElement.init()

    let api = Hook.useController<PostsController>()

    html
        $"""
        <p>Page: {api.Page}</p>
        <button @click={fun _ -> api.NextPage()}>Next Page</button>
        <ul>{posts}</li>
        """

```

For Classes:

```fsharp

type MyElement() =
    inherit LitElement()

    // the controller is initialized right away
    let api = PostsController jsThis

    let posts =
        api.Posts
        |> Lit.mapUnique (fun (p: Post) -> $"{p.id}") postTemplate

    override _.render() =
        html
            $"""
            <p>Page: {api.Page}</p>
            <button @click={fun _ -> api.NextPage()}>Next Page</button>
            <ul>{posts}</li>
            """
```

> **Note**: When using `Hook.useController<Type>()` since it is just a convenience function, you have to know the types of the parameters your controller needs
> since we pass the parameters straight to the contructor
> For example a controller with the following constructor `type MyController (host: LitElement, apiKey: string, userId: number)`, you will have to call it this way: `Hook.useController<MyController>("this is the api key", 123435)` but we don't have a way to ensure type safety in those cases, that's why the controllers option is available in the `LitElement.init` function which uses the controller directly so you know exactly which types you need to provide

### When to use controllers

Controllers lend themselves to be used when state is mutable or full of side effects, the controllers are just means to contain mutability while the rest of the code performs as usual

- Fetching data from a server.
- Updating the view when web socket information arrives.
- Updating the view when observable values change.
- Importing existing controllers from the ecosystem

### When not to use controllers

If you are using the function based API, if you need to process or transform data, the views are stateless or there are no complex side effects that can be handled within the `Hook.useEffect` hook you are likely better using hooks rather than controllers.

Controllers help you self-contain mutability and complexity while seamlessly blend within the Lit's native rendering mechanisms in some cases you endup with easier and simpler to maintain and read components.

That being said it is an optional feature that you may never require if it doesn't fit your style

## Elmish Controller

If you have used elmish with the function based API you will be happy to know that an Elmish controller has been provided out of the box, here's an example usage:

```fsharp
open Elmish
open Lit
open Lit.Elmish

type SampleState = { counter: int }
type SampleMsg =
    | Increment
    | Decrement

let sampleInit () = { counter = 0 }, Cmd.none

let sampleUpdate msg state =
    match msg with
    | Increment -> { state with counter = state.counter + 1 }, Cmd.none
    | Decrement -> { state with counter = state.counter - 1 }, Cmd.none

type ElementWithController() =
    inherit LitElement()

    let elmish = ElmishController(jsThis, sampleInit, sampleUpdate)

    override _.render() =
        html
            $"""
            <p>Elmish Controller Counter: {elmish.state.counter} </p>
            <button @click={fun _ -> elmish.dispatch Increment}>Increment</button>
            <button @click={fun _ -> elmish.dispatch Decrement}>Decrement</button>
            """


registerElement<ClassComponents.ElementWithController> "element-with-controller" {
    css $"p {{ color: red; }}"
    css $"li {{ color: rebeccapurple; }}"
}
```
