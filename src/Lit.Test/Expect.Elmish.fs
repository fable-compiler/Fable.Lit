module Expect.Elmish

open System
open System.Collections.Generic
open Elmish
open Lit
open Expect.Dom

/// Observable that keeps a copy of the last triggered value
/// and reports it immediately upon subscription.
type Store<'T>() =
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

/// Disposable that waits until the Disposable property is set if necessary/
/// Useful if we need to dispose when the disposable reference may have not been set yet
/// (e.g. when subscribing to a store).
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

type ObservableContainer<'T> =
    inherit Container
    inherit IObservable<'T>

module Program =
    /// Mounts an element to the DOM to render the Elmish app and returns the container
    /// as an observable that will notify of model changes.
    let runTestWith (arg: 'arg) (program: Program<'arg, 'model, 'msg, Lit.TemplateResult>) = promise {
        let store = Store<'model>()
        let! container = render_html $"<div></div>"

        let setState model dispatch =
            Program.view program model dispatch |> Lit.render container.El
            store.Trigger model

        Program.withSetState setState program
        |> Program.runWith arg

        return
            { new ObservableContainer<'model> with
                member _.El = container.El
                member _.Dispose() = container.Dispose()
                member _.Subscribe(w) = (store :> IObservable<_>).Subscribe(w) }
    }

    /// Mounts an element to the DOM to render the Elmish app and returns the container
    /// as an observable that will notify of model changes.
    let runTest (program: Program<unit, 'model, 'msg, Lit.TemplateResult>) =
        runTestWith () program
