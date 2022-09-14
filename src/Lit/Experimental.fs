[<Experimental("This is an exploration space, anything can be added or removed in this module at any point")>]
module Lit.Experimental


open Fable.Core
open Lit
open System

[<AttachMembers>]
type StateController<'T>(host, initial: 'T) =
    inherit ReactiveController(host)
    let mutable state = initial

    member _.Value = state

    member _.SetState(value: 'T) =
        state <- value
        host.requestUpdate ()
