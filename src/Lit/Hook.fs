namespace Lit

open System
open Fable.Core
open Fable.Core.JsInterop

[<AttachMembers>]
type HookDirective() =
    inherit AsyncDirective()

    let mutable _firstRun = true
    let mutable _args = [||]

    let mutable _stateIndex = 0
    let _states = ResizeArray<obj>()

    let mutable _refIndex = 0
    let _refs = ResizeArray<RefValue<obj>>()

    let _effects = ResizeArray<unit -> IDisposable>()
    let _disposables = ResizeArray<IDisposable>()

    member _.renderFn = Unchecked.defaultof<JS.Function>

    member this.createTemplate() =
        // Reset indices
        _stateIndex <- 0
        _refIndex <- 0

        this.renderFn.apply(this, _args)

    member this.render([<ParamArray>] args: obj[]) =
        _args <- args

        let result = this.createTemplate()
        if _firstRun then
            _firstRun <- false
            for effect in _effects do
                _disposables.Add(effect())

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

    member _.useRef<'T>(?value: 'T): RefValue<'T> =
        if _firstRun then
            let ref = Lit.createRef<'T>()
            value |> Option.iter (fun value -> ref.value <- Some(value))
            _refs.Add(unbox ref)
            ref
        else
            let idx = _refIndex
            _refIndex <- idx + 1
            unbox _refs.[idx]

    member _.useEffectOnce(effect: unit -> IDisposable): unit =
        if _firstRun then
            _effects.Add(effect)

    member _.disconnected() =
        for disp in _disposables do
            disp.Dispose()
        _disposables.Clear()

    member _.reconnected() =
        for effect in _effects do
             _disposables.Add(effect())

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

    static member inline useRef<'Value>(): RefValue<'Value> =
        jsThis<HookDirective>.useRef<'Value>()

    static member inline useRef(v: 'Value): RefValue<'Value> =
        jsThis<HookDirective>.useRef(v)

    static member inline useEffectOnce(effect: unit -> IDisposable) =
        jsThis<HookDirective>.useEffectOnce(effect)
