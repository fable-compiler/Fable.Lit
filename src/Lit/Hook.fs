namespace Lit

open System
open Fable.Core
open Fable.Core.JsInterop

[<RequireQualifiedAccess>]
type internal Effect =
    | OnConnected of (unit -> IDisposable)
    | EveryTime of (unit -> unit)

[<AttachMembers>]
type HookDirective() =
    inherit AsyncDirective()

    let mutable _firstRun = true
    let mutable _args = [||]

    let mutable _stateIndex = 0
    let _states = ResizeArray<obj>()

    let mutable _refIndex = 0
    let _refs = ResizeArray<RefValue<obj>>()

    let _effects = ResizeArray<Effect>()
    let _disposables = ResizeArray<IDisposable>()

    member _.renderFn = Unchecked.defaultof<JS.Function>

    member this.createTemplate() =
        // Reset indices
        _stateIndex <- 0
        _refIndex <- 0

        this.renderFn.apply(this, _args)

    member _.runEffects() =
        _effects |> Seq.iter (function
            | Effect.EveryTime effect -> effect()
            | Effect.OnConnected effect ->
                if _firstRun then
                    _disposables.Add(effect()))

    member this.render([<ParamArray>] args: obj[]) =
        _args <- args
        let result = this.createTemplate()
        this.runEffects()
        _firstRun <- false
        result

    member this.setState(index: int, value: 'T): unit =
        _states.[index] <- value
        // TODO: Should we check we're not in the middle of a render?
        this.createTemplate() |> this.setValue

    member this.useState(init: unit -> 'T): 'T * ('T -> unit) =
        let state, index =
            if _firstRun then
                let state = init()
                _states.Add(state)
                state, _states.Count - 1
            else
                let idx = _stateIndex
                _stateIndex <- idx + 1
                _states.[idx] :?> _, idx

        state, fun v -> this.setState(index, v)

    member _.useRef<'T>(init: unit -> 'T): RefValue<'T> =
        if _firstRun then
            let ref = Lit.createRef<'T>()
            ref.value <- init()
            _refs.Add(unbox ref)
            ref
        else
            let idx = _refIndex
            _refIndex <- idx + 1
            unbox _refs.[idx]

    member _.useEffect(effect): unit =
        if _firstRun then
            _effects.Add(Effect.EveryTime effect)

    member _.useEffectOnce(effect): unit =
        if _firstRun then
            _effects.Add(Effect.OnConnected effect)

    member _.disconnected() =
        for disp in _disposables do
            disp.Dispose()
        _disposables.Clear()

    member this.reconnected() =
        this.runEffects()

type HookComponentAttribute() =
    inherit JS.DecoratorAttribute()
    override _.Decorate(fn) =
        emitJsExpr (jsConstructor<HookDirective>, fn)
            "class extends $0 { renderFn = $1 }"
        |> LitHtml.directive :?> _

type Hook() =
    static member inline useState(v: 'Value) =
        jsThis<HookDirective>.useState(fun () -> v)

    static member inline useState(init: unit -> 'Value) =
        jsThis<HookDirective>.useState(init)

    static member inline useRef<'Value>(): RefValue<'Value option> =
        jsThis<HookDirective>.useRef<'Value option>(fun () -> None)

    static member inline useRef(v: 'Value): RefValue<'Value> =
        jsThis<HookDirective>.useRef(fun () -> v)

    static member inline useMemo(init: unit -> 'Value): 'Value =
        jsThis<HookDirective>.useRef(init).value

    // TODO: Dependencies?
    static member inline useEffect(effect: unit -> unit) =
        jsThis<HookDirective>.useEffect(effect)

    static member inline useEffectOnce(effect: unit -> IDisposable) =
        jsThis<HookDirective>.useEffectOnce(effect)

    static member createDisposable(f: unit -> unit) =
        { new IDisposable with
            member _.Dispose() = f() }

    static member emptyDisposable =
        { new IDisposable with
            member _.Dispose() = () }

    static member inline useCancellationToken () =
        Hook.useCancellationToken(jsThis)

    static member useCancellationToken (this: HookDirective) =
        let cts = this.useRef(fun () -> new Threading.CancellationTokenSource())
        let token = this.useRef(fun () -> cts.value.Token)

        this.useEffectOnce(fun () ->
            Hook.createDisposable(fun () ->
                cts.value.Cancel()
                cts.value.Dispose()
            )
        )

        token
