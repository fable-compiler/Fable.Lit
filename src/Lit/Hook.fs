namespace Lit

open System
open System.Collections.Generic
open Fable.Core
open Fable.Core.JsInterop
open Browser

module internal HookUtil =
    let createDisposable(f: unit -> unit) =
        { new IDisposable with
            member _.Dispose() = f () }

    let emptyDisposable =
        createDisposable ignore

    let delay ms f =
        JS.setTimeout f ms |> ignore

    [<RequireQualifiedAccess>]
    type Effect =
        | OnConnected of (unit -> IDisposable)
        | OnRender of (unit -> unit)

    [<Struct>]
    type RingState<'item> =
        | Writable of wx: 'item array * ix: int
        | ReadWritable of rw: 'item array * wix: int * rix: int

    type RingBuffer<'item>(size) =
        let doubleSize ix (items: 'item array) =
            seq {
                yield! items |> Seq.skip ix
                yield! items |> Seq.take ix

                for _ in 0 .. items.Length do
                    yield Unchecked.defaultof<'item>
            }
            |> Array.ofSeq

        let mutable state: 'item RingState =
            Writable(Array.zeroCreate (max size 10), 0)

        member _.Pop() =
            match state with
            | ReadWritable (items, wix, rix) ->
                let rix' = (rix + 1) % items.Length

                match rix' = wix with
                | true -> state <- Writable(items, wix)
                | _ -> state <- ReadWritable(items, wix, rix')

                Some items.[rix]
            | _ -> None

        member _.Push(item: 'item) =
            match state with
            | Writable (items, ix) ->
                items.[ix] <- item
                let wix = (ix + 1) % items.Length
                state <- ReadWritable(items, wix, ix)
            | ReadWritable (items, wix, rix) ->
                items.[wix] <- item
                let wix' = (wix + 1) % items.Length

                match wix' = rix with
                | true -> state <- ReadWritable(items |> doubleSize rix, items.Length, 0)
                | _ -> state <- ReadWritable(items, wix', rix)

open HookUtil

type TransitionState = HasLeft | AboutToEnter | Entering | HasEntered | Leaving

type TransitionConfig(ms: int, ?cssBefore: string, ?cssIdle: string, ?cssAfter: string, ?onComplete: bool -> unit) =
    member _.ms = ms
    member _.cssBefore = defaultArg cssBefore ""
    member _.cssIdle = defaultArg cssIdle ""
    member _.cssAfter =
        match cssAfter, cssBefore with
        | Some v, _ | _, Some v -> v
        | None, None -> ""
    member _.onComplete(isIn: bool) = match onComplete with Some f -> f isIn | None -> ()

type Transition =
    /// Indicates the current state of the state of the transition:
    /// `AboutToEnter | Entering | HasEntered | Leaving | HasLeft`
    abstract state: TransitionState
    /// Indicates whether the transition is currently entering or leaving.
    /// Useful to disable buttons, for example.
    abstract isRunning: bool
    /// Indicates whether the transition has already left.
    /// Note the transition doesn't remove/hide the element by itself,
    /// this has to be done in the `onComplete` event.
    abstract hasLeft: bool
    /// Gives the style string for the current state (before, idle or after).
    abstract css: string
    /// Trigger the enter `trigger(true)` or leave `trigger(false)` transition.
    abstract trigger: enter: bool -> unit

type Cmd<'Msg> = (('Msg -> unit) -> unit) list

type RenderFn = obj[] -> TemplateResult

[<AttachMembers>]
type HookContext(renderFn: RenderFn, triggerRender: HookContext -> unit , isConnected: unit -> bool) =
    let mutable _firstRun = true
    let mutable _rendering = false

    let mutable _stateIndex = 0
    let mutable _effectIndex = 0
    let _states = ResizeArray<obj>()
    let _effects = ResizeArray<Effect>()
    let _disposables = ResizeArray<IDisposable>()

    member val args = [||] with get, set

    // TODO: Improve error message for each situation
    member _.fail() =
        failwith "Hooks must be called consistently for each render call"

    member this.requestUpdate() =
        triggerRender(this)

    member this.render() =
        _stateIndex <- 0
        _effectIndex <- 0
        _rendering <- true

        let res = renderFn(this.args)

        if not _firstRun &&
            (_stateIndex <> _states.Count || _effectIndex <> _effects.Count) then
            this.fail ()

        _rendering <- false

        if isConnected() then
            this.runEffects (onRender = true, onConnected = _firstRun)

        _firstRun <- false
        res

    member this.checkRendering() =
        if not _rendering then this.fail ()

    member _.runAsync(f: unit -> unit) =
        // When using requestAnimationFrame some browsers (Firefox) skip renders
        // window.requestAnimationFrame (fun _ -> f ()) |> ignore
        JS.setTimeout f 0 |> ignore

    member this.runEffects(onConnected: bool, onRender: bool) =
        this.runAsync
            (fun () ->
                _effects
                |> Seq.iter
                    (function
                    | Effect.OnRender effect -> if onRender then effect ()
                    | Effect.OnConnected effect ->
                        if onConnected then
                            _disposables.Add(effect ())))

    member this.setState(index: int, newValue: 'T, ?equals: 'T -> 'T -> bool) : unit =
        let equals (oldValue: 'T) (newValue: 'T) =
            match equals with
            | Some equals -> equals oldValue newValue
            | None -> box(oldValue).Equals(newValue)

        let oldValue = _states.[index] :?> 'T

        if not (equals oldValue newValue) then
            _states.[index] <- newValue

            if not _rendering then
                triggerRender(this)
            else
                this.runAsync (fun () -> triggerRender(this))

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

    member this.useElmish(init, update) =
        if _firstRun then
            // TODO: Error handling? (also when running update)
            let exec dispatch cmd =
                cmd |> List.iter (fun call -> call dispatch)

            let (model, cmd) = init ()
            let index, (model, _) = this.addState (model, null)

            let setState (model: 'State) (dispatch: 'Msg -> unit) =
                this.setState (
                    index,
                    (model, dispatch),
                    equals = fun (oldModel, _) (newModel, _) -> (box oldModel).Equals(newModel)
                )

            let rb = RingBuffer 10
            let mutable reentered = false
            let mutable state = model

            let rec dispatch msg =
                if reentered then
                    rb.Push msg
                else
                    reentered <- true
                    let mutable nextMsg = Some msg

                    while Option.isSome nextMsg do
                        let msg = nextMsg.Value
                        let (model', cmd') = update msg state
                        setState model' dispatch
                        cmd' |> exec dispatch
                        state <- model'
                        nextMsg <- rb.Pop()

                    reentered <- false

            _effects.Add(
                Effect.OnConnected
                    (fun () ->
                        cmd |> exec dispatch

                        { new IDisposable with
                            member _.Dispose() =
                                let (state, _) = _states.[index] :?> _

                                match box state with
                                | :? IDisposable as disp -> disp.Dispose()
                                | _ -> () })
            )

            _states.[index] <- (state, dispatch)
            state, dispatch
        else
            _effectIndex <- _effectIndex + 1
            this.getState () |> snd

[<AllowNullLiteral>]
type IHookProvider =
    abstract hooks: HookContext

[<AutoOpen>]
module HookExtensions =
    type Transition with
        member this.triggerEnter() = this.trigger(true)
        member this.triggerLeave() = this.trigger(false)

    type HookContext with
        member ctx.useEffectOnce(effect: (unit -> unit)) =
            ctx.useEffectOnce(fun () ->
                effect()
                emptyDisposable)

        member ctx.useState(v: 'T) =
            ctx.useState(fun () -> v)

        member ctx.useRef(v: 'T) =
            ctx.useRef(fun () -> v)

        member ctx.useRef<'T>() =
            ctx.useRef(fun () -> None: 'T option)

        member ctx.useEffectOnChange(value: 'T, effect: 'T -> unit) =
            ctx.useEffectOnChange(value, fun v ->
                effect v
                emptyDisposable)

        member ctx.useEffectOnChange(value: 'T, effect: 'T -> IDisposable) =
            let prev = ctx.useRef<'T * IDisposable>()
            ctx.useEffect(fun () ->
                match prev.Value with
                | None ->
                    prev := Some(value, effect value)
                | Some(prevValue, disp) ->
                    if prevValue <> value then
                        disp.Dispose()
                        prev := Some(value, effect value)
            )

[<AttachMembers; AbstractClass>]
type HookDirective() =
    inherit AsyncDirective()
#if DEBUG
    let mutable _hmrSub: IDisposable option = None
#endif
    let mutable _hooks: HookContext option = None

    abstract renderFn: JS.Function with get, set
    abstract name: string

    member this.render([<ParamArray>] args: obj []) =
        let hooks = (this :> IHookProvider).hooks
        hooks.args <- args
        hooks.render ()

    member this.disconnected() =
#if DEBUG
        match _hmrSub with
        | None -> ()
        | Some d ->
            _hmrSub <- None
            d.Dispose()
#endif
        (this :> IHookProvider).hooks.disconnect()

    // In some situations, a disconnected part may be reconnected again,
    // so we need to re-run the effects but the old state is kept
    // https://lit.dev/docs/api/custom-directives/#AsyncDirective
    member this.reconnected() =
        (this :> IHookProvider).hooks.runEffects (onConnected = true, onRender = false)

#if DEBUG
    member this.subscribeHmr(token: HMRToken) =
        match _hmrSub with
        | Some _ -> ()
        | None ->
            _hmrSub <-
                token.Subscribe(fun updatedModule ->
                    this.renderFn <- updatedModule?(this.name)?renderFn)
                |> Some
#endif

    interface IHookProvider with
        member this.hooks =
            match _hooks with
            | Some hooks -> hooks
            | None ->
                let hooks =
                    HookContext(
                        (fun args -> this.renderFn.apply(jsThis, args) :?> _),
                        (fun ctx -> this.setValue(ctx.render())),
                        (fun () -> this.isConnected))
                _hooks <- Some hooks
                hooks

/// <summary>
/// Use this decorator to enable "stateful" functions
/// (i.e. functions that can use hooks like <see cref="Lit.Hook.useState">Hook.useState</see>)
/// </summary>
type HookComponentAttribute() =
#if !DEBUG
    inherit JS.DecoratorAttribute()
    override _.Decorate(renderFn) =
        emitJsExpr (jsConstructor<HookDirective>, renderFn)
            "class extends $0 { get renderFn() { return $1 } }"
        |> LitBindings.directive :?> _
#else
    inherit JS.ReflectedDecoratorAttribute()
    override _.Decorate(renderFn, mi) =
        let renderRef = LitBindings.createRef()
        renderRef.value <- renderFn
        let classExpr =
            emitJsExpr (jsConstructor<HookDirective>, renderRef, mi.Name) """
                class extends $0 {
                    get name() { return $2; }
                    get renderFn() { return $1.value; }
                    set renderFn(v) {
                        $1.value = v;
                        this.hooks.requestUpdate();
                    }
                }"""
        let directive = classExpr |> LitBindings.directive
        // This lets us access the updated render function when accepting new modules in HMR
        directive?renderFn <- renderFn
        directive :?> _
#endif

/// <summary>
/// A static class that contains react like hooks.
/// </summary>
/// <remarks>
/// These hooks use directives under the hood
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

    /// Pass the HMR token created with `HMR.createToken()` in **this same file** to activate HMR for this component.
    ///
    /// > Currently, only compatible with HookComponent (not LitElement).
    /// > When compiling in non-debug mode, this has no effect.
    static member inline useHmr(token: IHMRToken): unit =
#if !DEBUG
        ()
#else
        Hook.useHmr(token, jsThis)

    static member useHmr(token: IHMRToken, this: obj): unit =
        match token with
        | :? HMRToken as token ->
            match this with
            | :? HookDirective as directive -> directive.subscribeHmr(token)
            | _ -> JS.console.warn("useHmr can only be used with HookComponent")
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
        Hook.getContext().useRef(init).Value

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
    /// Start an [Elmish](https://elmish.github.io/elmish/) model-view-update loop.
    /// </summary>
    /// <example>
    ///      type State = { counter: int }
    ///
    ///      type Msg = Increment | Decrement
    ///
    ///      let init () = { counter = 0 }
    ///
    ///      let update msg state =
    ///          match msg with
    ///          | Increment -&gt; { state with counter = state.counter + 1 }
    ///          | Decrement -&gt; { state with counter = state.counter - 1 }
    ///
    ///      [&lt;HookComponent>]
    ///      let app () =
    ///          let state, dispatch = Hook.useElmish(init, update)
    ///         html $"""
    ///               &lt;header>Click the counter&lt;/header>
    ///               &lt;div id="count">{state.counter}&lt;/div>
    ///               &lt;button type="button" @click=${fun _ -> dispatch Increment}>
    ///                 Increment
    ///               &lt;/button>
    ///               &lt;button type="button" @click=${fun _ -> dispatch Decrement}>
    ///                   Decrement
    ///                &lt;/button>
    ///              """
    /// </example>
    static member inline useElmish<'State,'Msg when 'State : equality> (init: unit -> 'State * Cmd<'Msg>, update: 'Msg -> 'State -> 'State * Cmd<'Msg>): 'State * ('Msg -> unit) =
        Hook.getContext().useElmish(init, update)

    /// <summary>
    /// Helper to implement CSS transitions in your component.
    /// </summary>
    /// <param name="ms">The length of the transition in milliseconds.</param>
    /// <param name="cssBefore">The style to be applied before the item has entered.</param>
    /// <param name="cssIdle">The style to be applied after the item has entered (and before leaving).</param>
    /// <param name="cssAfter">The style to be applied when the item is about to leave (if omitted, `cssBefore` will also be applied when leaving).</param>
    /// <param name="onComplete">Event fired when the transition has completed. `true` is passed when the transition has entered, and `false` when it has left.</param>
    static member inline useTransition(ms, ?cssBefore, ?cssIdle, ?cssAfter, ?onComplete): Transition =
        Hook.useTransition(Hook.getContext(), TransitionConfig(ms, ?cssBefore=cssBefore, ?cssIdle=cssIdle, ?cssAfter=cssAfter, ?onComplete=onComplete))

    static member useTransition(ctx: HookContext, transition: TransitionConfig): Transition =
        let state, setState = ctx.useState(AboutToEnter)

        let trigger isIn =
            let middleState, finalState =
                if isIn then Entering, HasEntered
                else Leaving, HasLeft
            delay transition.ms (fun () ->
                setState finalState
                transition.onComplete(isIn)
            )
            setState middleState

        ctx.useEffectOnChange(state, function
            | AboutToEnter -> trigger true
            | _ -> ())

        { new Transition with
            member _.css =
                $"transition-duration: {transition.ms}ms; " +
                    match state with
                    | HasLeft | AboutToEnter -> transition.cssBefore
                    | Entering | HasEntered -> transition.cssIdle
                    | Leaving -> transition.cssAfter
            member _.state = state
            member _.isRunning =
                match state with
                | AboutToEnter | Entering | Leaving -> true
                | HasEntered | HasLeft -> false
            member _.hasLeft =
                match state with
                | HasLeft -> true
                | _ -> false
            member _.trigger(isIn) =
                if isIn then setState AboutToEnter
                else trigger false
        }
