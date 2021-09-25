---
title: Pipeline
layout: nacara-standard
---

## Introduction

Pipeline style allows you use to chain your promise using the pipe operator `|>`.

Writing your code using the pipeline style makes your code looks similar to what you would write in JavaScript.

<div class="columns" date-disable-copy-button="true">
    <div class="column is-half-desktop">

<div class="has-text-centered mb-2 has-text-weight-semibold">JavaScript</div>

```js
fetch('https://my-api.com/users')
.then(function (response) {
  return fetch('https://my-api.com/posts')
})
.then(function (response) {
    // Done, do something with the result
})
.catch(function (req) {
    // An error ocurred
})
```

</div>
    <div class="column is-half-desktop">

<div class="has-text-centered mb-2 has-text-weight-semibold">F#</div>

```fsharp
fetch "https://my-api.com/users"
|> Promise.map (fun response ->
    fetch "https://my-api.com/posts"
)
|> Promise.map (fun response ->
    // Done, do something with the result
)
|> Promise.catch (fun error ->
    // An error ocurred
)
```
</div>
</div>

## API

Description and examples for all the pipeline API.

### Promise.create

`create: f: (('T -> unit) -> (exn -> unit) -> unit) -> Promise<'T>`

Create a promise from a function.

```fsharp
let write (path: string) (content: string) =
    Promise.create (fun resolve reject ->
        Node.Api.fs.writeFile(path, content, (fun res ->
            match res with
            | Some res -> reject (res :?> System.Exception)
            | None -> resolve ()
        ))
    )
```

### Promise.sleep

`sleep: ms: int -> Promise<unit>`

Create a promise which wait `X` ms before resolving.

```fsharp
// Do something
doSomething ()
// Sleep for 1 second
|> Promise.sleep 1000
// Do another thing
|> Promise.map (fun _ ->
    doAnotherThing ()
)
|> Promise.map ...
```

### Promise.lift

`lift: a: 'T -> Promise<'T>`

Create a promise (in resolved state) with supplied value.

```fsharp
Promise.lift {| Firstname = "John" |}
|> Promise.map (fun user ->
    console.log $"Hello, %s{user.Firstname}"
    // Expected output: "Hello, John"
)
|> Promise.map ...
```

### Promise.reject

`reject: reason: exn -> Promise<'T>`

Create a promise (in rejected state) with supplied reason.

```fsharp
Promise.reject "User not found"
|> Promise.map (fun _ ->
    // This promise is skipped
)
|> Promise.catch (fun errorMessage ->
    console.error $"An error ocurred: %s{errorMessage}"
    // Expected output: "An error ocurred: User not found"
)
|> Promise.map ...
```

### Promise.bind

`bind: a : ('T1 -> JS.Promise<'T2>) -> pr: Promise<'T1> -> Promise<'T2>`

Bind a value into a promise of a new type.

```fsharp
Promise.lift {| Firstname = "John" |}
|> Promise.bind (fun user ->
    // Do something with user and returns a promise
    Promise.create (fun resolve reject ->
        resolve $"Hello, %s{user.Firstname}"
    )
)
|> Promise.map (fun message ->
    console.log message
    // Expected output: "Hello, John"
)
|> Promise.map ...
```

### Promise.map

`map: a : ('T1 -> 'T2) -> pr: Promise<'T1> -> Promise<'T2>`

Map a value into another type, the result will be wrapped in a promise for you.

```fsharp
Promise.lift {| Firstname = "John" |}
|> Promise.map (fun user ->
    user.Firstname
) // Returns a Promise<string> with the value "John"
|> Promise.map ...
```

### Promise.iter

`iter: a : ('T -> unit) -> pr: Promise<'T> -> unit`

Call a function with the result of a promise and stop the promise chain.

This is equivalent to `Promise.map ... |> ignore`

```fsharp
fetchUser ()
|> Promise.iter (fun user ->
    console.log "User firstname is user.Firstname"
) // unit
```

### Promise.catch

`catch: fail: (exn -> 'T) -> pr  : Promise<'T> -> Promise<'T>`

Handle an errored promise allowing you pass a return value.

This version of `catch` fakes a function returning just `'T`, as opposed to `Promise<'T>`. If you need to return `Promise<'T>`, use `catchBind`.

```fsharp
Promise.create (fun resolve reject ->
    reject (System.Exception "User not found")
)
|> Promise.catch (fun error ->
    // Log the error
    console.error error
    // Do something to recover from the error
    Error error.Message
)
|> Promise.map ...
```

### Promise.catchBind

`catchBind: fail: (Exception -> JS.Promise<'T>) -> pr : Promise<'T> -> Promise<'T>`

Handle an errored promise allowing to call a promise.

This version of `catch` expects a function returning `Promise<'T>` as opposed to just `'T`. If you need to return just `'T`, use `catch`.

```fsharp
Promise.create (fun resolve reject ->
    reject (System.Exception "User not found")
)
|> Promise.catchBind (fun error ->
    // Recover from the error, here we call another promise and returns it's result
    logErrorToTheServer error
)
|> Promise.map ...
```

### Promise.catchEnd

`catchEnd: fail: (exn -> unit) -> pr  : Promise<'T> -> unit`

Used to catch errors at the end of a promise chain.

```fsharp
Promise.create (fun resolve reject ->
    reject (System.Exception "User not found")
)
|> Promise.map (fun _ ->
    // ...
)
|> Promise.map (fun _ ->
    // ...
)
|> Promise.catchEnd (fun error ->
    // ...
) // Returns unit
```

### Promise.either

`either: success: ('T1 -> 'T2) -> fail : (exn -> 'T2) -> pr : Promise<'T1> -> Promise<'T2>`

A combination of `map` and `catch`, this function applies the `success` continuation when the input promise resolves successfully, or `fail` continuation when the input promise fails.

```fsharp
somePromise
|> Promise.either
    (fun x -> string x)
    (fun err -> Promise.lift err.Message)
|> Promise.map ...
```

### Promise.eitherBind

`eitherBind: success: ('T1 -> JS.Promise<'T2>) -> fail : (exn -> JS.Promise<'T2>) -> pr : Promise<'T1> -> Promise<'T2>`

A combination of `bind` and `catchBind`, this function applies the `success` continuation when the input promise resolves successfully, or `fail` continuation when the input promise fails.

```fsharp
somePromise
|> Promise.eitherBind
    (fun x -> string x |> Promise.lift)
    (fun err -> Promise.lift err.Message)
|> Promise.map ...
```

### Promise.eitherEnd

`eitherEnd: success: ('T -> unit) -> fail : ('E -> unit) -> pr : Promise<'T> -> unit`

Same as [`Promise.either`](#Promise.either) but stopping the promise execution.

```fsharp
somePromise
|> Promise.eitherEnd
    (fun x -> string x)
    (fun err -> Promise.lift err.Message)
```

### Promise.start

`start: pr: Promise<'T> -> unit`

Start a promise.

In version before XXX, it was used because the promise CE was originally cold, so it didn't start until `then` was called. Now it's hot same as native promises, so `Promise.start` is equivalent to `promise |> ignore`

```fsharp
myPromise
|> Promise.start
```

### Promise.tryStart

`tryStart: fail: (exn -> unit) -> pr : Promise<'T> -> unit`

Same as [Promise.start](#Promise.start) but forcing you to handle the rejected state.

```fsharp
myPromise
|> Promise.tryStart
    (fun error ->
        // Do something on error
    )
```

### Promise.Parallel

`Parallel: pr: seq<JS.Promise<'T>> -> Promise<'T array>`

Same as [Promise.all](#Promise.all)

```fsharp
let p1 =
    promise {
        do! Promise.sleep 100
        return 1
    }
let p2 =
    promise {
        do! Promise.sleep 200
        return 2
    }
let p3 =
    promise {
        do! Promise.sleep 300
        return 3
    }

Promise.Parallel [p1; p2; p3]
|> Promise.map (fun res ->
    // res = [|1; 2; 3 |]
)
|> Promise.map ...
```

### Promise.all

`all: pr: seq<JS.Promise<'T>> -> Promise<'T array>`

`Promise.all` takes a sequence of promises as an input, and returns a single `Promise` that resolves to an array of the results of the input promises.

```fsharp
let p1 =
    promise {
        do! Promise.sleep 100
        return 1
    }
let p2 =
    promise {
        do! Promise.sleep 200
        return 2
    }
let p3 =
    promise {
        do! Promise.sleep 300
        return 3
    }

Promise.all [p1; p2; p3]
|> Promise.map (fun res ->
    // res = [|1; 2; 3 |]
)
|> Promise.map ...
```

Note: If you need to return mixed types you can use boxing and unboxing

```fsharp
let users =
    promise {
        let! users = fetchUsers ()
        return box users
    }
let posts =
    promise {
        let! posts = fetchPosts ()
        return box posts
    }

Promise.all [p1; p2]
|> Promise.map (fun res ->
    let users = unbox<User list> res.[0]
    let posts = unbox<Post list> res.[1]
    // ...
)
```

### Promise.result

`result: a: Promise<'T> -> Promise<Result<'T,exn>>`

Map the `Promise` result into a `Result` type.

```fsharp
// Success example
Promise.lift 42
|> Promise.result
|> Promise.map (fun value ->
    // value = Ok 42
)

// Fail example
Promise.reject "Invalid value"
|> Promise.result
|> Promise.map (fun value ->
    // value = Error "Invalid value"
)
```

### Promise.mapResult

`mapResult: fn: ('T1 -> 'T2) -> a : Promise<Result<'T1,'E>> -> Promise<Result<'T2,'E>>`

Evaluates to `myPromise |> Promise.map (Result.map fn)`

```fsharp
Promise.lift 42
|> Promise.result
|> Promise.mapResult (fun value ->
    value + 10
)
|> Promise.map (fun value ->
    // value = Ok 52
)
```

### Promise.bindResult

`bindResult: fn: ('T1 -> JS.Promise<'T2>) -> a : Promise<Result<'T1,'E>> -> Promise<Result<'T2,'E>>`

Transform the success part of a result promise into another promise.

```fsharp
let multiplyBy2 (value : int) =
    Promise.create (fun resolve reject ->
        resolve (value * 2)
    )

Promise.lift 42
|> Promise.result
|> Promise.bindResult (fun value ->
    multiplyBy2 value
)
|> Promise.map (fun value ->
    // value = Ok 84
)
```

### Promise.mapResultError

`mapResultError: fn: ('E1 -> 'E2) -> a : Promise<Result<'T,'E1>> -> Promise<Result<'T,'E2>>`

Evaluates to `myPromise |> Promise.map (Result.map fn)`

```fsharp
Promise.reject -1
|> Promise.result
|> Promise.mapResultError (fun value ->
    $"%s{value} is not a valid value"
)
|> Promise.map (fun value ->
    // value = Error "-1 is not a valid value"
)
```

### Promise.tap

`tap: fn: ('A -> unit) -> a : Promise<'A> -> Promise<'A>`

This is an identity function, it calls the given function and return the promise value untouched.

```fsharp
fetchUser ()
|> Promise.tap (fun user ->
    // Do something
    console.log "The user has been received"
)
|> Promise.map (fun user ->
    // user value is available here untouched
)
```
