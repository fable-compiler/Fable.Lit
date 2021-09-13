namespace Lit

open System
open Fable.Core
open Fable.Core.JsInterop
open Browser

module internal HookUtil =
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

type Cmd<'Msg> = (('Msg -> unit) -> unit) list

type IHookProvider =
    abstract useState: init: (unit -> 'T) -> 'T * ('T -> unit)
    abstract useRef: init: (unit -> 'T) -> RefValue<'T>
    abstract useEffect: effect: (unit -> unit) -> unit
    abstract useEffectOnce: effect: (unit -> IDisposable) -> unit
    abstract useElmish : init: (unit -> 'State * Cmd<'Msg>) * update: ('Msg -> 'State -> 'State * Cmd<'Msg>) -> 'State * ('Msg -> unit)

type RenderFn = delegate of [<ParamArray>] args: obj[] -> TemplateResult

[<AttachMembers>]
type HookProvider(renderFn: RenderFn, triggerRender: Action<HookProvider>, isConnected: Func<bool>) =
    let mutable _firstRun = true
    let mutable _rendering = false

    let mutable _stateIndex = 0
    let _states = ResizeArray<obj>()

    let _effects = ResizeArray<Effect>()
    let _disposables = ResizeArray<IDisposable>()

    member val args = [||] with get, set

    // TODO: Improve error message for each situation
    member _.fail() =
        failwith "Hooks must be called consistently for each render call"

    member this.render() =
        _stateIndex <- 0
        _rendering <- true
        let res = renderFn.Invoke(this.args)
        // TODO: Do same check for effects?
        if not _firstRun && _stateIndex <> _states.Count then
            this.fail ()

        _rendering <- false

        if isConnected.Invoke() then
            this.runEffects (onRender = true, onConnected = _firstRun)

        _firstRun <- false
        res

    member this.checkRendering() = if not _rendering then this.fail ()

    member _.runAsync(f: unit -> unit) =
        // JS.setTimeout f 0 |> ignore
        window.requestAnimationFrame (fun _ -> f ())
        |> ignore

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
                triggerRender.Invoke(this)
            else
                this.runAsync (fun () -> triggerRender.Invoke(this))

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

    member this.useRef(init: unit -> 'T) : RefValue<'T> =
        this.checkRendering ()

        if _firstRun then
            init ()
            |> Lit.createRef<'T>
            |> this.addState
            |> snd
        else
            this.getState () |> snd

    member _.useEffect(effect) : unit =
        if _firstRun then
            _effects.Add(Effect.OnRender effect)

    member _.useEffectOnce(effect) : unit =
        if _firstRun then
            _effects.Add(Effect.OnConnected effect)

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
            this.getState () |> snd

[<AttachMembers>]
type HookDirective() =
    inherit AsyncDirective()
    let provider =
        HookProvider(
            emitJsExpr () "(...args) => this.renderFn.apply(this, args)",
            emitJsExpr () "(provider) => this.setValue(provider.render())",
            emitJsExpr () "() => this.isConnected")

    member this.render([<ParamArray>] args: obj []) =
        provider.args <- args
        provider.render ()

    member _.disconnected() =
        provider.disconnect()

    // In some situations, a disconnected part may be reconnected again,
    // so we need to re-run the effects but the old state is kept
    // https://lit.dev/docs/api/custom-directives/#AsyncDirective
    member this.reconnected() =
        provider.runEffects (onConnected = true, onRender = false)

    interface IHookProvider with
        member _.useState(init) = provider.useState(init)
        member _.useRef(init) = provider.useRef(init)
        member _.useEffect(effect) = provider.useEffect(effect)
        member _.useEffectOnce(effect) = provider.useEffectOnce(effect)
        member _.useElmish(init, update) = provider.useElmish(init, update)

/// <summary>
/// Use this decorator to enable "stateful" functions
/// (i.e. functions that can use hooks like <see cref="Lit.Hook.useState">Hook.useState</see>)
/// </summary>
type HookComponentAttribute() =
    inherit JS.DecoratorAttribute()

    override _.Decorate(fn) =
        emitJsExpr (jsConstructor<HookDirective>, fn) "class extends $0 { renderFn = $1 }"
        |> LitBindings.directive
        :?> _

/// <summary>
/// A static class that contains react like hooks.
/// </summary>
/// <remarks>
/// These hooks use directives under the hood
/// and may not be 100% compatible with the react hooks.
/// </remarks>
type Hook() =
    static member createDisposable(f: unit -> unit) =
        { new IDisposable with
            member _.Dispose() = f () }

    static member emptyDisposable =
        { new IDisposable with
            member _.Dispose() = () }

    /// <summary>
    /// returns a tuple with an immutable value and a setter function for the provided value
    /// </summary>
    /// <example>
    ///     let counter, setCounter = Hook.useState 0
    /// </example>
    /// <param name="v">
    ///  the initial value of the state
    /// </param>
    static member inline useState(v: 'Value) =
        jsThis<IHookProvider>.useState (fun () -> v)

    /// <summary>
    /// returns a tuple with an immutable value and a setter function, when you supply a callback it will be used
    /// to initialize the value but it will not be called again
    /// </summary>
    /// <example>
    ///     let counter, setCounter = Hook.useState (fun _ -> expensiveInitializationLogic(0))
    /// </example>
    /// <param name="init">
    /// A function to initialize the state, usually this function may perform expensive operations
    /// </param>
    static member inline useState(init: unit -> 'Value) =
        jsThis<IHookProvider>.useState (init)

    /// <summary>
    /// Creates and returns a mutable object (a 'ref') whose .current property is initialized to the hosting element.
    /// This differs from useState in that state is immutable and can only be changed via setState which will cause a rerender.
    /// That rerender will allow you to be able to see the updated state value. A ref, on the other hand, can only be changed via
    /// .current and since changes to it are mutations, no rerender is required to view the updated value in your component's code (e.g. listeners, callbacks, effects).
    /// </summary>
    static member inline useRef<'Value>(): RefValue<'Value option> =
        jsThis<IHookProvider>.useRef<'Value option>(fun () -> None)

    /// <summary>
    /// Creates and returns a mutable object (a 'ref') whose .current property is initialized to the passed argument.
    /// This differs from useState in that state is immutable and can only be changed via setState which will cause a rerender.
    /// That rerender will allow you to be able to see the updated state value. A ref, on the other hand, can only be changed via
    /// .current and since changes to it are mutations, no rerender is required to view the updated value in your component's code (e.g. listeners, callbacks, effects).
    /// </summary>
    static member inline useRef(v: 'Value): RefValue<'Value> =
        jsThis<IHookProvider>.useRef(fun () -> v)

    /// <summary>
    /// Create a memoized state value. Only reruns the function when dependent values have changed.
    /// </summary>
    static member inline useMemo(init: unit -> 'Value): 'Value =
        jsThis<IHookProvider>.useRef(init).value

    // TODO: Dependencies?
    /// <summary>
    /// Used to run a side-effect when the component re-renders.
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
    static member inline useEffect(effect: unit -> unit) =
        jsThis<IHookProvider>.useEffect (effect)

    /// <summary>
    /// Fire a side effect once in the lifetime of the function
    /// </summary>
    /// <example>
    ///     Hook.useEffectOnce (fun _ -> printfn "Mounted")
    /// </example>
    static member inline useEffectOnce(effect: unit -> unit) =
        jsThis<IHookProvider>.useEffectOnce
            (fun () ->
                effect ()
                Hook.emptyDisposable)

    /// <summary>
    /// Fire a side effect once in the lifetime of the function
    /// </summary>
    /// <example>
    ///     Hook.useEffectOnce (fun _ -> { new IDisposable with member _.Dispose() = (* code *))})
    /// </example>
    static member inline useEffectOnce(effect: unit -> IDisposable) =
        jsThis<IHookProvider>.useEffectOnce (effect)

    /// <summary>
    /// Start an [Elmish](https://elmish.github.io/elmish/) loop. for a function.
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
    static member inline useElmish<'State,'Msg when 'State : equality> (init: unit -> 'State * Cmd<'Msg>, update: 'Msg -> 'State -> 'State * Cmd<'Msg>) =
        jsThis<IHookProvider>.useElmish(init, update)

    static member inline useCancellationToken() =
        Hook.useCancellationToken (jsThis)

    static member useCancellationToken(this: IHookProvider) =
        let cts =
            this.useRef (fun () -> new Threading.CancellationTokenSource())

        let token = this.useRef (fun () -> cts.value.Token)

        this.useEffectOnce
            (fun () ->
                Hook.createDisposable
                    (fun () ->
                        cts.value.Cancel()
                        cts.value.Dispose()))

        token
