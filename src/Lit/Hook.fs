namespace Lit

open System
open Fable.Core
open Fable.Core.JsInterop

module internal HookUtil =
    type Cmd<'Msg> = (('Msg -> unit) -> unit) list

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

[<AttachMembers>]
type HookDirective() =
    inherit AsyncDirective()

    let mutable _firstRun = true
    let mutable _rendering = false
    let mutable _args = [||]

    let mutable _stateIndex = 0
    let _states = ResizeArray<obj>()

    let _effects = ResizeArray<Effect>()
    let _disposables = ResizeArray<IDisposable>()

    member _.renderFn = Unchecked.defaultof<JS.Function>

    // TODO: Improve error message for each situation
    member _.fail() =
        failwith "Hooks must be called consistently for each render call"

    member this.createTemplate() =
        _stateIndex <- 0
        _rendering <- true
        let res = this.renderFn.apply (this, _args)
        // TODO: Do same check for effects?
        if not _firstRun && _stateIndex <> _states.Count then
            this.fail ()

        _rendering <- false

        if this.isConnected then
            this.runEffects (onRender = true, onConnected = _firstRun)

        _firstRun <- false
        res

    member this.checkRendering() = if not _rendering then this.fail ()

    member _.runAsync(f: unit -> unit) = JS.setTimeout f 0 |> ignore

    member this.runEffects(onConnected: bool, onRender: bool) =
        // lit-html doesn't provide a didUpdate callback so just use a 0 timeout.
        this.runAsync
            (fun () ->
                _effects
                |> Seq.iter
                    (function
                    | Effect.OnRender effect -> if onRender then effect ()
                    | Effect.OnConnected effect ->
                        if onConnected then
                            _disposables.Add(effect ())))

    member this.render([<ParamArray>] args: obj []) =
        _args <- args
        this.createTemplate ()

    member this.setState(index: int, newValue: 'T, ?equals: 'T -> 'T -> bool) : unit =
        let equals (oldValue: 'T) (newValue: 'T) =
            match equals with
            | Some equals -> equals oldValue newValue
            | None -> box(oldValue).Equals(newValue)

        let oldValue = _states.[index] :?> 'T

        if not (equals oldValue newValue) then
            _states.[index] <- newValue

            if not _rendering then
                this.createTemplate () |> this.setValue
            else
                this.runAsync (fun () -> this.createTemplate () |> this.setValue)

    member this.getState() : int * 'T =
        if _stateIndex >= _states.Count then
            this.fail ()

        let idx = _stateIndex
        _stateIndex <- idx + 1
        idx, _states.[idx] :?> _

    member _.addState(state: 'T) : int * 'T =
        _states.Add(state)
        _states.Count - 1, state

    member this.useState(init: unit -> 'T) : 'T * ('T -> unit) =
        this.checkRendering ()

        let index, state =
            if _firstRun then
                init () |> this.addState
            else
                this.getState ()

        state, (fun v -> this.setState (index, v))

    member this.useRef<'T>(init: unit -> 'T) : RefValue<'T> =
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

    member this.useElmish
        (
            init: unit -> 'State * Cmd<'Msg>,
            update: 'Msg -> 'State -> 'State * Cmd<'Msg>
        ) : 'State * ('Msg -> unit) =
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
                    equals = fun (oldModel, _) (newModel, _) -> oldModel = newModel
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

    member _.disconnected() =
        for disp in _disposables do
            disp.Dispose()

        _disposables.Clear()

    // In some situations, a disconnected part may be reconnected again,
    // so we need to re-run the effects but the old state is kept
    // https://lit.dev/docs/api/custom-directives/#AsyncDirective
    member this.reconnected() =
        this.runEffects (onConnected = true, onRender = false)

type HookComponentAttribute() =
    inherit JS.DecoratorAttribute()

    override _.Decorate(fn) =
        emitJsExpr (jsConstructor<HookDirective>, fn) "class extends $0 { renderFn = $1 }"
        |> LitHtml.directive
        :?> _

type Hook() =
    static member inline useState(v: 'Value) =
        jsThis<HookDirective>.useState (fun () -> v)

    static member inline useState(init: unit -> 'Value) = jsThis<HookDirective>.useState (init)

    static member inline useRef<'Value>() : RefValue<'Value option> =
        jsThis<HookDirective>
            .useRef<'Value option> (fun () -> None)

    static member inline useRef(v: 'Value) : RefValue<'Value> =
        jsThis<HookDirective>.useRef (fun () -> v)

    static member inline useMemo(init: unit -> 'Value) : 'Value =
        jsThis<HookDirective>.useRef(init).value

    // TODO: Dependencies?
    static member inline useEffect(effect: unit -> unit) =
        jsThis<HookDirective>.useEffect (effect)

    static member inline useEffectOnce(effect: unit -> unit) =
        jsThis<HookDirective>.useEffectOnce
            (fun () ->
                effect ()
                Hook.emptyDisposable)

    static member inline useEffectOnce(effect: unit -> IDisposable) =
        jsThis<HookDirective>.useEffectOnce (effect)

    static member createDisposable(f: unit -> unit) =
        { new IDisposable with
            member _.Dispose() = f () }

    static member emptyDisposable =
        { new IDisposable with
            member _.Dispose() = () }

    static member inline useElmish<'State, 'Msg when 'State: equality>
        (
            init: unit -> 'State * Cmd<'Msg>,
            update: 'Msg -> 'State -> 'State * Cmd<'Msg>
        ) =
        jsThis<HookDirective>.useElmish (init, update)

    static member inline useCancellationToken() = Hook.useCancellationToken (jsThis)

    static member useCancellationToken(this: HookDirective) =
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
