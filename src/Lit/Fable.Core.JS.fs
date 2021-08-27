module Fable.Core.JS

open System

type [<AllowNullLiteral>] Function =
    abstract apply: thisArg: obj * args: obj[] -> obj
    abstract bind: thisArg: obj * [<ParamArray>] args: obj[] -> Function
    abstract call: thisArg: obj * [<ParamArray>] args: obj[] -> obj
    [<Emit "$0($1...)">] abstract Invoke: [<ParamArray>] args: obj[] -> obj
    [<Emit "new $0($1...)">] abstract Create: [<ParamArray>] args: obj[] -> obj

[<AbstractClass>]
type DecoratorAttribute() =
    inherit Attribute()
    abstract Decorate: fn: Function * fnName: string * [<ParamArray>] attributeArgs: obj[] -> Function

// Hack because currently doesn't keep information about spread for anonymous function
// We also use function to make sure `this` is bound correctly
[<Emit("function (...args) { return $0(args) }")>]
let spreadFunc (fn: obj[] -> obj): Function = jsNative
