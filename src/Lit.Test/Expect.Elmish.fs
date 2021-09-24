module Expect.Elmish

open System
open System.Collections.Generic
open Elmish
open Lit
open Expect.Dom

type Observable<'T>() =
    let mutable value: 'T option = None
    let listeners = Dictionary<Guid, IObserver<'T>>()
    member _.Trigger(v) =
        value <- Some v
        for l in listeners.Values do
            l.OnNext(v)
    interface IObservable<'T> with
        member _.Subscribe(w) =
            value |> Option.iter w.OnNext
            let g = Guid.NewGuid()
            listeners.Add(g, w)
            { new IDisposable with
                member _.Dispose() = listeners.Remove(g) |> ignore }

type LazyDisposable() =
    let mutable _disposed = false
    let mutable _disposable: IDisposable option = None
    member _.Disposable with set(d: IDisposable) =
        match _disposed, _disposable with
        | false, Some _ -> failwith "Item was already assigned a disposable"
        | true, Some _ -> failwith "Item is already disposed"
        | false, None -> _disposable <- Some d
        | true, None ->
            _disposable <- Some d
            d.Dispose()
    member _.Dispose() =
        _disposed <- true
        _disposable |> Option.iter (fun d -> d.Dispose())

type IObservable<'T> with
    member obs.Await() =
        Promise.create(fun resolve _ ->
            let disp = LazyDisposable()
            disp.Disposable <- obs.Subscribe(fun v ->
                disp.Dispose()
                resolve v))

module Program =
    let runTest (program: Program<unit, 'model, 'msg, Lit.TemplateResult>) = promise {
        let obs = Observable<'model>()
        let! el = render_html $"<div></div>"

        let setState model dispatch =
            Program.view program model dispatch |> Lit.render el.El
            obs.Trigger model

        Program.withSetState setState program
        |> Program.run

        return el, obs :> IObservable<_>
    }
