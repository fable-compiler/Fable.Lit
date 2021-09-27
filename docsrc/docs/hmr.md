---
title: Hot Reloading
layout: nacara-standard
---

## Webpack

Webpack has been the standard to build frontend apps for a while, and it's also widely used in Fable apps. Webpack is extremely reliable and extensible and it

## Vite

Vite is a bit opinionated and thanks to this you can get a development server and build tool with zero-config by conforming to a few defaults:

- Put your static files in the `public` folder
- Put your `index.html` in the app root (usually next to `package.json`)
- Add a reference to the entry JS file (relative path is important):

```html
    <script type="module" src="./build/client/App.js"></script>
```

With this you only need to install Vite and run the commands for starting the development server or bundling for production as needed.

```
npm i -D vite
npx vite --open     # Start dev server and open browser
npx vite build      # Build your site for production
```

That's it, now you have an extremely fast development server and an optimized build with zero config (built files will be put in `dist` folder by default). If you need to edit some settings, you can still use a [config file](https://vitejs.dev/config/) or the CLI options (check them with `npx vite --help`).

### Fable --runFast

If you use `--runFast` option with Fable instead of `--run`, Vite will be started right away instead of waiting until first Fable compilation. When developing you usually already have the compiled files from the previous day, so this way you can get a development server up and running in under 200 milliseconds!

```
dotnet fable watch src -o build/client --runFast vite --open
```

> When Fable compilation finishes Vite will just reload with the updated code.

For production make sure to use `--run` so vite doesn't start bundling until Fable has finished compilation.

```
dotnet fable src -o build/client --run vite build
```

## Non-bundling HMR

Lit.Elmish.HMR works with non-bundling development servers like Vite or Snowpack, but with a important caveat. These dev servers do "true" Hot-Module-Replacement in the sense they only replace the updated module (remember in JS "modules" is roughly the same as "file"). However, in Elmish apps the function that triggers rendering `Program.run` is usually located in the last file. When updating any other file, the render function won't be triggered and you won't see anything changing on screen.

Solving this for Vite and Snowpack requires injecting special code in each module containing a component. This is usually done by plugins like [React Fast Refresh](https://github.com/vitejs/vite/tree/main/packages/plugin-react). Indeed you can already get a hyper-fast hot reloading experience out-of-the-box with Vite/Snowpack thanks to [Feliz ReactComponent](https://zaid-ajaj.github.io/Feliz/#/Feliz/React/StatelessComponents). At the time of writing, there is no such a plugin for Lit, instead Fable.Lit provides code helpers for [HookComponents](./hook-components.html) so you can control HMR at the component level.

:::info
At the time of writing HMR is **only available for HookComponents** not for web components declared with `LitElement`.
:::

First thing you need to do is to instantiate a private _HMR token_ at the top of the file containing your HookComponents:

```fsharp
open Lit

let hmr = HMR.createToken()
```

One important thing to remember though, is that HMR in non-bundling dev servers doesn't just update the whole module (this wouldn't have any effect because other modules would still reference the old exports). In the case of Fable.Lit it will just update the render function of the HookComponents, so in order to avoid breaking dependent modules it's a good idea to put internal helpers (together with the HMR token) within a private inner module, and only expose the HookComponents.

```fsharp
module private Util =
    let hmr = HMR.createToken()
    // Other utilities here

open Util

[<HookComponent>]
let MyComponent() = ..
```

:::info
This limitation is the same as with React Fast Refresh. The main difference is with Fable.Lit you can have multiple HookComponents in the same file.
:::

Now the only thing left is to pass the HMR token to the HookComponents, this has to be done for each component you want to enable HMR for, and can be done with the `useHmr` hook.

```fsharp
[<HookComponent>]
let MyComponent() =
    Hook.useHmr(hmr)
    ..
```

This is a bit of a set up but it will enable a blazing fast feedback loop that is extremely valuable when you're making small UI adjustments. Also, when compiling in Release mode the HMR helpers will automatically be removed, so you don't need to worry about disabling them manually.

<hr />

If for some reason you are getting unexpected behavior with HMR, you can disable it and force a full refresh by passing `false` when creating the HMR token in a specific module.

```fsharp
let hmr = HMR.createToken(false)
```

> After passing false, you need to save **twice** before the full refresh kicks in.
