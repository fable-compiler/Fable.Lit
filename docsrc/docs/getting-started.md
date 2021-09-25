---
title: Getting started
layout: nacara-standard
---

Add `Fable.Promise` package to your project.

If you are using nuget to manage you dependencies:

`dotnet add package Fable.Promise`

If you are using [paket](https://fsprojects.github.io/Paket/):

`dotnet paket add Fable.Promise`

You are ready to go, you can directly access `Promise` module or the `promise` computation.

Example:

```fsharp
let helloPromise =
    Promise.create (fun resolve reject ->
        resolve "Hello, from a promise"
    )

// Pipeline style
helloPromise
|> Promise.iter (fun message ->
    printfn message
    // Output: Hello, from a promise
)

// Using computation expression
promise {
    let! message = helloPromise
    printfn message
    // Output: Hello, from a promise
}
```
