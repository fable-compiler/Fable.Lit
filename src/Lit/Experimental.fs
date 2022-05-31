[<Experimental("This is an exploration space, anything can be added or removed in this module at any point")>]
module Lit.Experimental


open Fable.Core
open Fable.Core.JsInterop
open Lit

[<AttachMembers>]
type StateController<'T>(host, initial: 'T) =
    inherit ReactiveController(host)
    let mutable state = initial

    member _.Value = state

    member _.SetState(value: 'T) =
        state <- value
        host.requestUpdate ()

[<AttachMembers>]
type TwoArgsCtrl(host, initial: int, secondial: int) =
    inherit ReactiveController(host)

    member val Values = (initial, secondial) with get, set

    member this.updateValues(initial, secondial) =
        this.Values <- (initial, secondial)
        host.requestUpdate ()


type Controllers =

    static member inline GetController<'T>([<System.ParamArray>] args: obj array) : 'T =
        jsThis?getOrAddController (jsConstructor<'T>, args)

    static member inline GetProperty(propName: string, ?initial: 'T) : Types.RefValue<'T> =
        jsThis?getOrAddProperty (propName, initial)

[<Literal>]
let CLS_EXPR =
    """class extends $0 {
        constructor() {
            super();
            this.renderFn = $1
        }

        render() {
            return this.renderFn.value.apply(this, [])
        }

        getOrAddController(controller, args) {
            if(!this[controller.name]) {
                return this[controller.name] = new controller(this, ...args)
            }
            return this[controller.name];
        }

        getOrAddProperty(propname, initial) {
            if(!this[propname]) {
                this[propname] = initial;
                return this[propname];
            }
            return this[propname];
        }
    }
    """

type LitElementExperimental(useShadowDom: bool) =
    inherit JS.DecoratorAttribute()

    override this.Decorate(fn: JS.Function) : JS.Function =
        let renderRef = LitBindings.createRef ()
        renderRef.value <- fn
        let baseClass = emitJsExpr (jsConstructor<LitElement>, renderRef) (CLS_EXPR)

        baseClass
