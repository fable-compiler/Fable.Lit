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

type HookComponentAttribute() =
    inherit JS.DecoratorAttribute()

    override _.Decorate(fn) =
        emitJsExpr (jsConstructor<HookDirective>, fn) "class extends $0 { renderFn = $1 }"
        |> LitHtml.directive
        :?> _

type Hook() =
    static member createDisposable(f: unit -> unit) =
        { new IDisposable with
            member _.Dispose() = f () }

    static member emptyDisposable =
        { new IDisposable with
            member _.Dispose() = () }

    static member inline useState(v: 'Value) =
        jsThis<IHookProvider>.useState (fun () -> v)

    static member inline useState(init: unit -> 'Value) =
        jsThis<IHookProvider>.useState (init)

    static member inline useRef<'Value>() : RefValue<'Value option> =
        jsThis<IHookProvider>
            .useRef<'Value option> (fun () -> None)

    static member inline useRef(v: 'Value) : RefValue<'Value> =
        jsThis<IHookProvider>.useRef (fun () -> v)

    static member inline useMemo(init: unit -> 'Value) : 'Value =
        jsThis<IHookProvider>.useRef(init).value

    // TODO: Dependencies?
    static member inline useEffect(effect: unit -> unit) =
        jsThis<IHookProvider>.useEffect (effect)

    static member inline useEffectOnce(effect: unit -> unit) =
        jsThis<IHookProvider>.useEffectOnce
            (fun () ->
                effect ()
                Hook.emptyDisposable)

    static member inline useEffectOnce(effect: unit -> IDisposable) =
        jsThis<IHookProvider>.useEffectOnce (effect)

    static member inline useElmish(init, update) =
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
