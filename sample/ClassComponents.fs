module ClassComponents

open Lit
open Fable.Core
open Fetch

type UserProfile() =
    inherit LitElement()

    let name = ""
    let age = 0

    override _.render() =
        html $"<p>{name} - {age}</p>"


type MouseController =
    inherit ReactiveController
    abstract x: float
    abstract y: float

[<Import("MouseController", from="./controllers.js"); Emit("new $0($1)")>]
let private createMouseController (host: LitElement): MouseController = jsNative

type Post = {| usedId: int; id: int; title: string; body: string |}

type ApiController(host: LitElement) as this =
    do 
        host.addController(this)

    member val Posts: Post array = Array.empty with get, set
    member val Page: int = 1 with get, set
    member val Limit: int = 10 with get, set

    member _.FetchUsers(?page: int, ?limit: int) =
        let page = defaultArg page this.Page
        let limit = defaultArg limit this.Limit
        promise {
            let! posts = 
                promise {
                    let! result = tryFetch $"https://jsonplaceholder.typicode.com/posts?_page={page}&_limit={limit}" []
                    match result with 
                    | Ok res ->
                        let! result = res.json() :?> JS.Promise<Post array>
                        return result
                    | Error err -> return Array.empty<Post>
                }

            this.Posts <- posts
            host.requestUpdate()
        }

    member _.NextPage() =
        this.Page <- this.Page + 1
        this.FetchUsers() |> Promise.start

    interface ReactiveHostConnected with
        member this.hostConnected(): unit = 
            this.FetchUsers() |> Promise.start



type ElementWithController() as this =
    inherit LitElement()

    let mouse = createMouseController this
    let api = ApiController this
    
    let posts (post: Post) = 
        html $"<li>{post.title}</li>"

    override _.render() =
        html $"""
            <p>{mouse.x} - {mouse.y}</p>
            <p>Page: {api.Page}</p>
            <button @click={fun _ -> api.NextPage()}>Next Page</button>
            <ul>{Lit.mapUnique (fun (p: Post) -> $"{p.id}") posts api.Posts}</li>
        """