namespace Lit

open System
open Fable.Core
open Fable.Core.JsInterop
open Browser

type Prop<'T>(defaultValue: 'T) =
    member _.DefaultValue = defaultValue
    [<Emit("$0")>]
    member _.Value = defaultValue

type ILitElInit =
    abstract init: props: obj * ?styles: Styles -> obj

type LitElInitData =
    {| props: obj; styles: Styles option |}

type LitElInit() =
    static member fail() = failwith "LitElement.init must be called on top of the render function"

    member val data: LitElInitData option = None with get, set

    interface ILitElInit with
        member this.init(props, ?styles) =
            this.data <- Some {| props = props; styles = styles |}
            box()

    interface IHookProvider with
        member this.useState(init) = LitElInit.fail()
        member this.useRef(init) = LitElInit.fail()
        member this.useEffect(effect) = LitElInit.fail()
        member this.useEffectOnce(effect) = LitElInit.fail()
        member this.useElmish(init, update) = LitElInit.fail()

[<AttachMembers>]
type LitHookElement(renderFn: JS.Function, initProps: JS.Function) =
    inherit LitElementBase()
    do initProps.Invoke([|jsThis|]) |> ignore
    let provider =
        HookProvider(
            emitJsExpr renderFn "() => $0.apply(this)",
            emitJsExpr () "() => this.requestUpdate()",
            emitJsExpr () "() => this.isConnected")

    member this.render() =
        provider.render()

    member _.disconnectedCallback() =
        base.disconnectedCallback()
        provider.disconnect()

    member this.connectedCallback() =
        base.connectedCallback()
        provider.runEffects (onConnected = true, onRender = false)

    interface ILitElInit with
        member this.init(props, ?styles: Styles) = box this

    interface IHookProvider with
        member _.useState(init) = provider.useState(init)
        member _.useRef(init) = provider.useRef(init)
        member _.useEffect(effect) = provider.useEffect(effect)
        member _.useEffectOnce(effect) = provider.useEffectOnce(effect)
        member _.useElmish(init, update) = provider.useElmish(init, update)

type LitElementAttribute(name: string) =
    inherit JS.DecoratorAttribute()

    [<Emit("customElements.define($1, $2)")>]
    member _.defineCustomElement (name: string, cons: obj) = ()

    [<Emit("Object.defineProperty($1, $2, { get: $3 })")>]
    member _.defineGetter(target: obj, name: string, f: unit -> 'V) = ()

    override this.Decorate(renderFn) =
        let init = LitElInit()
        try
            renderFn.apply(init, [||]) |> ignore
        with _ -> ()

        match init.data with
        | None -> LitElInit.fail()
        | Some data ->
            let propsDic =
                // TODO: Check all values are of Prop type
                Option.ofObj data.props
                |> Option.map (fun props ->
                    (JS.Constructors.Object.keys(data.props),
                     JS.Constructors.Object.values(data.props))
                    ||> Seq.zip
                    |> dict)

            let initProps (this: obj) =
                match propsDic with
                | None -> ()
                | Some propsDic ->
                    propsDic |> Seq.iter(fun (KeyValue(k, p)) ->
                        this?(k) <- (p :?> Prop<obj>).DefaultValue)

            let classExpr =
                emitJsExpr (jsConstructor<LitHookElement>, renderFn, initProps) """class extends $0 {
                    constructor() {
                        super($1, $2);
                    }
                }"""

            match propsDic with
            | None -> ()
            | Some propsDic ->
                this.defineGetter(classExpr, "properties", fun () ->
                    (obj(), propsDic) ||> Seq.fold (fun o (KeyValue(k, p)) ->
                        o?(k) <- obj() // TODO: Property settings
                        o))

            match data.styles with
            | None -> ()
            | Some styles ->
                this.defineGetter(classExpr, "styles", fun () -> styles)

            // Register custom element
            this.defineCustomElement(name, classExpr)
            box(fun () -> failwith $"{name} is not immediately callable, it must be created in HTML") :?> _

type LitEl =
    // TODO: Allow unit props
    static member inline init(props: 'Props, ?styles: Styles) =
        jsThis<ILitElInit>.init(props, ?styles=styles) :?> 'Props