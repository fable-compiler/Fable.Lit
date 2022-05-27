namespace Lit

open System
open Fable.Core
open Fable.Core.JsInterop

module internal HookUtil =
    let [<Literal>] RENDER_FN_CLASS_EXPR =
        """class extends $0 {
            constructor() { super($2...) }
            get renderFn() { return $1 }
        }"""

    let [<Literal>] HMR_CLASS_EXPR =
        """class extends $0 {
            constructor() { super($3...) }
            get __name() { return $2; }
            get renderFn() { return $1.value; }
            set renderFn(v) {
                $1.value = v;
                this.hooks.requestUpdate();
            }
        }"""

    // From https://stackoverflow.com/a/6248722/17433542
    let generateShortUid(): string =
        emitJsStatement () """
        const firstPart = (Math.random() * 46656) | 0;
        const secondPart = (Math.random() * 46656) | 0;
        return "_" + firstPart.toString(36) + secondPart.toString(36);
        """

    let cssClasses = JS.Constructors.WeakMap.Create<obj, string>()

    let createDisposable(f: unit -> unit) =
        { new IDisposable with
            member _.Dispose() = f () }

    let emptyDisposable =
        createDisposable ignore

    let delay ms f =
        JS.setTimeout f ms |> ignore

    let runAsync(f: unit -> unit) =
        // When using requestAnimationFrame some browsers (Firefox) skip renders
        // window.requestAnimationFrame (fun _ -> f ()) |> ignore
        delay 0 f

    [<RequireQualifiedAccess>]
    type Effect =
        | OnConnected of (unit -> IDisposable)
        | OnRender of (unit -> unit)

    type RenderFn = obj[] -> TemplateResult

open HookUtil
open HMRTypes
open Types

type TransitionState = HasLeft | AboutToEnter | Entering | HasEntered | Leaving

type Transition =
    /// Gives the class name for the current state: "transition-enter", "transition-leave" or empty string.
    abstract className: string
    /// Indicates the current state of the state of the transition:
    /// `AboutToEnter | Entering | HasEntered | Leaving | HasLeft`
    abstract state: TransitionState
    /// Trigger the enter transition.
    abstract triggerEnter: unit -> unit
    /// Trigger the leave transition.
    abstract triggerLeave: unit -> unit

type HookContextHost =
    abstract renderFn: JS.Function
    abstract requestUpdate: unit -> unit
    abstract isConnected: bool

[<AttachMembers>]
type HookContext(host: HookContextHost) =
    let mutable _firstRun = true
    let mutable _rendering = false
    let mutable _args = [||]

    let mutable _stateIndex = 0
    let mutable _effectIndex = 0
    let _states = ResizeArray<obj>()
    let _effects = ResizeArray<Effect>()
    let _disposables = ResizeArray<IDisposable>()

    member _.host: obj = upcast host

    // TODO: Improve error message for each situation
    member _.fail() =
        failwith "Hooks must be called consistently for each render call"

    member _.requestUpdate() =
        host.requestUpdate()

    member this.renderWith(args: obj array) =
        if _firstRun || args <> _args then
            _args <- args
            this.render() |> Some
        else None

    member this.render(): TemplateResult =
        _stateIndex <- 0
        _effectIndex <- 0
        _rendering <- true

        let res = host.renderFn.apply(host, _args)

        if not _firstRun &&
            (_stateIndex <> _states.Count || _effectIndex <> _effects.Count) then
            this.fail ()

        _rendering <- false

        if host.isConnected then
            this.runEffects (onRender = true, onConnected = _firstRun)

        _firstRun <- false
        res :?> TemplateResult

    member this.checkRendering() =
        if not _rendering then this.fail ()

    member _.runEffects(onConnected: bool, onRender: bool) =
        runAsync(fun () ->
            _effects |> Seq.iter (function
                | Effect.OnRender effect -> if onRender then effect ()
                | Effect.OnConnected effect ->
                    if onConnected then
                        _disposables.Add(effect ())))

    member _.setState(index: int, newValue: 'T, ?equals: 'T -> 'T -> bool) : unit =
        let equals (oldValue: 'T) (newValue: 'T) =
            match equals with
            | Some equals -> equals oldValue newValue
            | None -> box(oldValue).Equals(newValue)

        let oldValue = _states.[index] :?> 'T

        if not (equals oldValue newValue) then
            _states.[index] <- newValue

            if not _rendering then
                host.requestUpdate()
            else
                runAsync host.requestUpdate

    member this.getState() : int * 'T =
        if _stateIndex >= _states.Count then
            this.fail ()

        let idx = _stateIndex
        _stateIndex <- idx + 1
        idx, _states.[idx] :?> _

    member _.addState(state: 'T) : int * 'T =
        _states.Add(state)
        _states.Count - 1, state

    member _.disconnect() =
        for disp in _disposables do
            disp.Dispose()

        _disposables.Clear()

    member this.useState(init: unit -> 'T) : 'T * ('T -> unit) =
        this.checkRendering ()

        let index, state =
            if _firstRun then
                init () |> this.addState
            else
                this.getState ()

        state, (fun v -> this.setState (index, v))

    member this.useRef(init: unit -> 'T) : ref<'T> =
        this.checkRendering ()

        if _firstRun then
            init ()
            |> ref
            |> this.addState
            |> snd
        else
            this.getState () |> snd

    member private this.setEffect(effect) : unit =
        this.checkRendering ()

        if _firstRun then
            _effects.Add(effect)
        else
            if _effectIndex >= _effects.Count then
                this.fail ()

            let idx = _effectIndex
            _effectIndex <- idx + 1
            _effects.[idx] <- effect

    member this.useEffect(effect) : unit =
        this.setEffect(Effect.OnRender effect)

    member this.useEffectOnce(effect) : unit =
        this.setEffect(Effect.OnConnected effect)

[<AllowNullLiteral>]
type IHookProvider =
    abstract hooks: HookContext

[<AutoOpen>]
module HookExtensions =
    type Transition with
        /// Indicates whether the transition has already left.
        /// Note the transition doesn't remove/hide the element by itself,
        /// this has to be done in the `onComplete` event.
        member this.hasLeft =
            match this.state with
            | HasLeft -> true
            | _ -> false

        /// Indicates whether the transition is currently entering or leaving.
        /// Useful to disable buttons, for example.
        member this.isRunning =
            match this.state with
            | AboutToEnter | Entering | Leaving -> true
            | HasEntered | HasLeft -> false

    type HookContext with
        member ctx.useState(v: 'T) =
            ctx.useState(fun () -> v)

        member ctx.useRef(v: 'T) =
            ctx.useRef(fun () -> v)

        member ctx.useRef<'T>() =
            ctx.useRef(fun () -> None: 'T option)

        member ctx.useMemo(init: unit -> 'Value): 'Value =
            ctx.useRef(init).Value

        member ctx.useEffectOnce(effect: (unit -> unit)) =
            ctx.useEffectOnce(fun () ->
                effect()
                emptyDisposable)

        member ctx.useEffectOnChange(value: 'T, effect: 'T -> unit) =
            ctx.useEffectOnChange(value, fun v ->
                effect v
                emptyDisposable)

        member ctx.useEffectOnChange(value: 'T, effect: 'T -> IDisposable) =
            let prev = ctx.useRef<'T * IDisposable>()
            ctx.useEffect(fun () ->
                match prev.Value with
                | None ->
                    prev.Value <- Some(value, effect value)
                | Some(prevValue, disp) ->
                    if prevValue <> value then
                        disp.Dispose()
                        prev.Value <- Some(value, effect value)
            )

        member ctx.useTransition(ms: int, ?onEntered: unit -> unit, ?onLeft: unit -> unit): Transition =
            let state, setState = ctx.useState(AboutToEnter)

            let trigger isIn =
                let middleState, finalState =
                    if isIn then Entering, HasEntered
                    else Leaving, HasLeft
                delay ms (fun () ->
                    setState finalState
                    let f = if isIn then onEntered else onLeft
                    f |> Option.iter (fun f -> f())
                )
                setState middleState

            ctx.useEffectOnChange(state, function
                | AboutToEnter -> trigger true
                | _ -> ())

            { new Transition with
                member _.state = state
                member _.className =
                    match state with
                    | HasLeft | AboutToEnter -> "transition-enter"
                    | Entering | HasEntered -> ""
                    | Leaving -> "transition-leave"
                member _.triggerEnter() = setState AboutToEnter
                member _.triggerLeave() = trigger false
            }

        member ctx.remove_css(): unit =
            let proto = JS.Constructors.Object.getPrototypeOf(ctx.host)
            let cssClass = cssClasses.get(proto)
            if not(isNull cssClass) then
                cssClasses.delete(proto) |> ignore
                let doc = Browser.Dom.document
                let headEl = doc.querySelector("head")
                headEl.removeChild(doc.getElementById(cssClass)) |> ignore

        member ctx.use_scoped_css(rules: string): string =
            let proto = JS.Constructors.Object.getPrototypeOf(ctx.host)
            let className = cssClasses.get(proto)
            if isNull className then
                // TODO: In debug, it'd be nice to prefix this with the name of the component
                let className = generateShortUid()
                let doc = Browser.Dom.document
                let styleEl = doc.createElement("style")
                styleEl.setAttribute("id", className)
                rules
                |> Parser.Css.scope className
                |> doc.createTextNode
                |> styleEl.appendChild
                |> ignore
                doc.querySelector("head").appendChild(styleEl) |> ignore
                cssClasses.set(proto, className) |> ignore
                className
            else
                className

[<AttachMembers; AbstractClass>]
type HookDirective() =
    inherit AsyncDirective()
    let _hooks = HookContext(jsThis)
#if DEBUG
    let mutable _hmrSub: IDisposable option = None
#endif

    abstract renderFn: JS.Function with get, set
    abstract __name: string

    member this.requestUpdate() =
        this.setValue(_hooks.render())

    member _.render([<ParamArray>] args: obj []) =
        match _hooks.renderWith(args) with
        | Some template -> template
        | None -> LitBindings.noChange

    member _.disconnected() =
#if DEBUG
        match _hmrSub with
        | None -> ()
        | Some d ->
            _hmrSub <- None
            d.Dispose()
#endif
        _hooks.disconnect()

    // In some situations, a disconnected part may be reconnected again,
    // so we need to re-run the effects but the old state is kept
    // https://lit.dev/docs/api/custom-directives/#AsyncDirective
    member _.reconnected() =
        _hooks.runEffects (onConnected = true, onRender = false)

#if DEBUG
    interface HMRSubscriber with
        member this.subscribeHmr = Some <| fun token ->
            match _hmrSub with
            | Some _ -> ()
            | None ->
                _hmrSub <-
                    token.Subscribe(fun info ->
                        _hooks.remove_css()
                        let updatedModule = info.NewModule
                        this.renderFn <- updatedModule?(this.__name)?renderFn
                    ) |> Some
#endif

    interface IHookProvider with
        member _.hooks = _hooks

/// <summary>
/// Use this decorator to enable "stateful" functions
/// (i.e. functions that can use hooks like <see cref="Lit.Hook.useState">Hook.useState</see>)
/// </summary>
type HookComponentAttribute() =
#if !DEBUG
    inherit JS.DecoratorAttribute()
    override _.Decorate(renderFn) =
        emitJsExpr (jsConstructor<HookDirective>, renderFn) RENDER_FN_CLASS_EXPR
        |> LitBindings.directive :?> _
#else
    inherit JS.ReflectedDecoratorAttribute()
    override _.Decorate(renderFn, mi) =
        let renderRef = LitBindings.createRef()
        renderRef.value <- renderFn
        let classExpr =
            emitJsExpr (jsConstructor<HookDirective>, renderRef, mi.Name) HMR_CLASS_EXPR
        let directive = classExpr |> LitBindings.directive
        // This lets us access the updated render function when accepting new modules in HMR
        directive?renderFn <- renderFn
        directive :?> _
#endif

/// <summary>
/// A static class that contains react like hooks.
/// </summary>
/// <remarks>
/// These hooks use Lit directives under the hood
/// and may not be 100% compatible with the react hooks.
/// </remarks>
type Hook() =
    /// Use `getContext()`
    static member getContext(this: IHookProvider) =
        if isNull this || not(box this.hooks :? HookContext) then
            failwith "Cannot access hook context, make sure the hook is called on top of a HookComponent function"
        this.hooks

    /// Only call `getContext` from an inlined function when implementing a custom hook
    static member inline getContext() =
        Hook.getContext(jsThis)

    static member createDisposable(f: unit -> unit) = createDisposable f

    static member emptyDisposable = emptyDisposable

    /// <summary>
    /// Returns a tuple with an immutable value and a setter function for the provided value
    /// </summary>
    /// <example>
    ///     let counter, setCounter = Hook.useState 0
    /// </example>
    /// <param name="v">
    ///  the initial value of the state
    /// </param>
    static member inline useState(v: 'Value) =
        Hook.getContext().useState (fun () -> v)

    /// <summary>
    /// Returns a tuple with an immutable value and a setter function, when you supply a callback it will be used
    /// to initialize the value but it will not be called again
    /// </summary>
    /// <example>
    ///     let counter, setCounter = Hook.useState (fun _ -> expensiveInitializationLogic(0))
    /// </example>
    /// <param name="init">
    /// A function to initialize the state, usually this function may perform expensive operations
    /// </param>
    static member inline useState(init: unit -> 'Value) =
        Hook.getContext().useState (init)

    /// <summary>
    /// Pass the HMR token created with `HMR.createToken()` in **this same file** to activate HMR for this component.
    /// Only has effect when compiling in debug mode.
    /// </summary>
    /// <remarks>
    /// Currently, only compatible with Vitejs
    /// </remarks>
    /// <param name="token">
    /// Token created with `HMR.createToken()` in **this same file**.
    /// </param>
    static member inline useHmr(token: IHMRToken): unit =
        Hook.useHmr(token, jsThis)

    static member useHmr(token: IHMRToken, this: HMRSubscriber): unit =
#if !DEBUG
        ()
#else
        match token, this.subscribeHmr with
        | :? HMRToken as token, Some subscribe -> subscribe(token)
        | _ -> ()
#endif

    /// <summary>
    /// Creates and returns a mutable object (a 'ref') whose .current property is initialized to the hosting element.
    /// This differs from useState in that state is immutable and can only be changed via setState which will cause a rerender.
    /// That rerender will allow you to be able to see the updated state value. A ref, on the other hand, can only be changed via
    /// .current and since changes to it are mutations, no rerender is required to view the updated value in your component's code (e.g. listeners, callbacks, effects).
    /// </summary>
    static member inline useRef<'Value>(): ref<'Value option> =
        Hook.getContext().useRef<'Value option>(fun () -> None)

    /// <summary>
    /// Creates and returns a mutable object (a 'ref') whose .current property is initialized to the passed argument.
    /// This differs from useState in that state is immutable and can only be changed via setState which will cause a rerender.
    /// That rerender will allow you to be able to see the updated state value. A ref, on the other hand, can only be changed via
    /// .current and since changes to it are mutations, no rerender is required to view the updated value in your component's code (e.g. listeners, callbacks, effects).
    /// </summary>
    static member inline useRef(v: 'Value): ref<'Value> =
        Hook.getContext().useRef(fun () -> v)

    /// <summary>
    /// Create a memoized state value. Only reruns the function when dependent values have changed.
    /// </summary>
    static member inline useMemo(init: unit -> 'Value): 'Value =
        Hook.getContext().useMemo(init)

    /// <summary>
    /// Used to run a side-effect each time after the component renders.
    /// </summary>
    /// <example>
    ///     [&lt;HookComponent>]
    ///     let app () =
    ///         let counter, setCounter = Hook.useState 0
    ///         Hook.useEffect (fun _ -> printfn "log to the console on every re-render")
    ///         html $"""
    ///             &lt;header>Click the counter&lt;/header>
    ///             &lt;div id="count">{counter}&lt;/div>
    ///             &lt;button type="button" @click=${fun _ -> setCount(counter + 1)}>
    ///               Cause rerender
    ///             &lt;/button>
    ///        """
    /// </example>
    static member inline useEffect(effect: unit -> unit): unit =
        Hook.getContext().useEffect(effect)

    /// <summary>
    /// Fire a side effect once in the lifetime of the function.
    /// </summary>
    /// <example>
    ///     Hook.useEffectOnce (fun _ -> printfn "Mounted")
    /// </example>
    static member inline useEffectOnce(effect: unit -> unit): unit =
        Hook.getContext().useEffectOnce
            (fun () ->
                effect ()
                Hook.emptyDisposable)

    /// <summary>
    /// Fire a side effect once in the lifetime of the function.
    /// The disposable will be run when the item is disconnected (removed from DOM by Lit).
    /// </summary>
    /// <example>
    ///     Hook.useEffectOnce (fun _ -> { new IDisposable with member _.Dispose() = (* code *))})
    /// </example>
    static member inline useEffectOnce(effect: unit -> IDisposable): unit =
        Hook.getContext().useEffectOnce (effect)

    /// Fire a side effect after the component renders if the given value changes.
    /// The disposable will be run before running a new effect.
    static member inline useEffectOnChange(value: 'T, effect: 'T -> IDisposable): unit =
        Hook.getContext().useEffectOnChange(value, effect)

    /// Fire a side effect after the component renders if the given value changes.
    static member inline useEffectOnChange(value: 'T, effect: 'T -> unit): unit =
        Hook.getContext().useEffectOnChange(value, effect)

    /// <summary>
    /// Helper to implement CSS transitions in your component. It will give you the class name
    /// corresponding to current state: `transition-enter`, `transition-leave` (or empty string).
    /// It will also fire events when transitions complete.
    /// </summary>
    /// <remarks>
    /// You need to set `transition-duration` with the same length in your CSS.
    /// </remarks>
    /// <param name="ms">The length of the transition in milliseconds.</param>
    /// <param name="onEntered">Event fired when the enter transition has completed.</param>
    /// <param name="onLeft">Event fired when the leave transition has completed.</param>
    static member inline useTransition(ms, ?onEntered, ?onLeft): Transition =
        Hook.getContext().useTransition(ms, ?onEntered=onEntered, ?onLeft=onLeft)

    /// <summary>
    /// Use scoped CSS when you cannot use shadow DOM. The hook will scope the rules
    /// with a unique id and will attach the styles to the document's `head`.
    /// **Returns a class name to be set in the root of your component.**
    /// </summary>
    /// <remarks>
    /// You can use `:host` in your CSS. @keyframes names will also be scoped. Compatible with HMR.
    /// </remarks>
    static member inline use_scoped_css(rules: string): string =
        Hook.getContext().use_scoped_css(rules)
