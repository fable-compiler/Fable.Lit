---
title: Getting started
layout: nacara-standard
---

## Requirements

Fable.Lit packages require **fable 3.3** dotnet tool and **Lit 2** from npm.

```
dotnet tool update fable
npm install lit
```

Then, in the directory of your .fsproj, install the packages you need. Note the package ids are prefixed by `Fable.` but not the actual namespaces.

```
dotnet add package Fable.Lit
dotnet add package Fable.Lit.Elmish
dotnet add package Fable.Lit.Test
```

## Scaffolding

To get up-to-speed faster you can just clone [Lit.TodoMVC](https://github.com/alfonsogarciacaro/Lit.TodoMVC), which already includes HTML templates, hook components, hot reloading and testing, and use it as the base for your app.
