---
title: Getting started
layout: nacara-standard
---

## Requirements

Fable.Lit packages require **fable 3.6** dotnet tool and **Lit 2** from npm.

## Installation

In the directory of your .fsproj, install the packages you need. Note the package ids are prefixed by `Fable.` but not the actual namespaces.

:::info
We recommend using [Femto](https://github.com/Zaid-Ajaj/Femto) so Nuget and NPM dependencies are installed automatically.
:::

```
dotnet femto install Fable.Lit
dotnet femto install Fable.Lit.Elmish
dotnet femto install Fable.Lit.Test
```

## Scaffolding

To get up-to-speed faster you can just clone [Lit.TodoMVC](https://github.com/alfonsogarciacaro/Lit.TodoMVC), which already includes HTML templates, hook components, hot reloading and testing, and use it as the base for your app.
