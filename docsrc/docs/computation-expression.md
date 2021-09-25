---
title: Computation expression
layout: nacara-standard
---

## Introduction

The `promise` computation expression makes it really easy to create and compose promise using F#.

<div class="columns" date-disable-copy-button="true">
    <div class="column is-half-desktop">

<div class="has-text-centered mb-2 has-text-weight-semibold">Pipeline API</div>

```fsharp
fetch "https://x.x/users"
|> Promise.map (fun response ->
    fetch "https://x.x/posts"
)
|> Promise.map (fun response ->
    // Done, do something with the result
)
```

</div>
    <div class="column is-half-desktop">

<div class="has-text-centered mb-2 has-text-weight-semibold">Computation expression</div>

```fsharp
promise {
    let! users = fetch "https://x.x/users"
    let! posts = fetch "https://x.x/posts"

    // Done, do something with the result
    return //...
}
```

</div>
</div>

## Guides

Here is a quick guides of what you can do with `promise` computations.

You can read more about computation expression in F# [here](https://docs.microsoft.com/en-us/dotnet/fsharp/language-reference/computation-expressions).

### Create a promise

Creating a promise is as simple as writing `promise { }`.

```fsharp
let double (value : int) =
    promise {
        return value * 2
    }
```

### Chaining promises

If you need the result of a promise before calling another one, use the `let!` keyword

```fsharp
promise {
    let! user = fetchUsers session
    let! permission = fetchPermission user

    return permission
}
```

You can also directly return the result of a promise avoiding to use `let!` and `return`

`return!` will evaluate the promise and return the result value when completed

```fsharp
promise {
    let! user = fetchUsers session

    return! fetchPermission user
}
```

### Nesting promises

You can nest `promise` computation as needed.

```fsharp
promise {
    // Nested promise which returns a value
    let! isValid =
        promise {
            // Do something
            return aValue
        }

    // Nested promise which return unit
    do! promise {
        // Do something
        return ()
    }
}
```

### Parallel

If you have independent promise, you can use `let!` and `and!` to run them in parallel.

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

promise {
    let! a = p1
    and! b = p2
    and! c = p3

    return a + b + c // 1 + 2 + 3 = 6
}
```

### Support non-native promise (aka Thenable)

In JavaScript, a thenable is an object that has a `then()` function. All promises are thenables, but not all thenables are promises.

For example, this is the case when working on a VSCode extensions, or with mongoose, axios.

[Source](https://masteringjs.io/tutorials/fundamentals/thenable)

You can extends the `promise` extension to make it easy to works with your thenable.

Example:

```fsharp
/// This is the definition of a thenable from ts2fable's generation VsCode API
type [<AllowNullLiteral>] Thenable<'T> =
    abstract ``then``: ?onfulfilled: ('T -> U2<'TResult, Thenable<'TResult>>) * ?onrejected: (obj option -> U2<'TResult, Thenable<'TResult>>) -> Thenable<'TResult>
    abstract ``then``: ?onfulfilled: ('T -> U2<'TResult, Thenable<'TResult>>) * ?onrejected: (obj option -> unit) -> Thenable<'TResult>

module Thenable =
    // Transform a thenable into a promise
    let toPromise (t: Thenable<'t>): JS.Promise<'t> =  unbox t

type Promise.PromiseBuilder with
    /// To make a value interop with the promise builder, you have to add an
    /// overload of the `Source` member to convert from your type to a promise.
    /// Because thenables are trivially convertible, we can just unbox them.
    member x.Source(t: Thenable<'t>): JS.Promise<'t> = Thenable.toPromise t

    // Also provide these cases for overload resolution
    member _.Source(p: JS.Promise<'T1>): JS.Promise<'T1> = p
    member _.Source(ps: #seq<_>): _ = ps

// You can now works with instance of Thenable from the promise computation

// Dummy thenable for the example
let sampleThenable () =
    promise {
        return 1
    }
    |> Thenable.ofPromise

// See how you can use the thenable directly from the promise computation
promise {
    let! initialValue = sampleThenable()
    // ...
}
```
