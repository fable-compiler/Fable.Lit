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

type EffectKind =
    | Callback
    | Disposable of IDisposable

[<AttachMembers>]
type EffectController
    (
        host,
        ?onConnected: LitElement -> EffectKind,
        ?onUpdate: LitElement -> EffectKind,
        ?onUpdated: LitElement -> EffectKind,
        ?onDisconnected: LitElement -> EffectKind
    ) =
    inherit ReactiveController(host)
    let disposables = ResizeArray<IDisposable>()

    override _.hostConnected() =
        match onConnected with
        | Some onConnected ->
            match onConnected host with
            | Disposable disposable -> disposables.Add disposable
            | _ -> ()
        | _ -> ()

    override _.hostUpdate() =
        match onUpdate with
        | Some onUpdate ->
            match onUpdate host with
            | Disposable disposable -> disposables.Add disposable
            | _ -> ()
        | _ -> ()

    override _.hostUpdated() =
        match onUpdated with
        | Some onUpdated ->
            match onUpdated host with
            | Disposable disposable -> disposables.Add disposable
            | _ -> ()
        | _ -> ()

    override _.hostDisconnected() =
        match onDisconnected with
        | Some onDisconnected ->
            match onDisconnected host with
            | Disposable disposable -> disposables.Add disposable
            | _ -> ()
        | _ -> ()

        disposables |> Seq.iter (fun d -> d.Dispose())
