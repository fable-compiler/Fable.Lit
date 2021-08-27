namespace Lit

open System
open Fable.Core
open Fable.Core.JsInterop

[<AttachMembers>]
type HookDirective() =
    inherit AsyncDirective()

    let renderFn = Unchecked.defaultof<JS.Function>

    let mutable _firstRun = true
    let mutable _args = [||]

    let mutable _stateIndex = 0
    let _states = ResizeArray<obj>()

    member this.createTemplate() =
        // Reset indices
        _stateIndex <- 0

        renderFn.apply(this, _args)

    member this.render([<ParamArray>] args: obj[]) =
        _args <- args

        let result = this.createTemplate()
        if _firstRun then
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

type HookComponentAttribute() =
    inherit JS.DecoratorAttribute()
    override _.Decorate(fn, _fnName, _arg) =
        emitJsExpr (jsConstructor<HookDirective>, fn) "class extends $0 { renderFn = $1 }"
        |> LitHtml.directive :?> _

type Hook() =
    static member inline useState(v: 'Value) =
        jsThis<HookDirective>.useState(fun () -> v)

    static member inline useState(init: unit -> 'Value) =
        jsThis<HookDirective>.useState(init)
