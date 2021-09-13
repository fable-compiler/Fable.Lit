namespace Lit

open Fable.Core
open Fable.Core.JsInterop

module private LitElementUtil =
    let isDefined (x: obj) = not(isNull x)
    let failInit() = failwith "LitElement.init must be called on top of the render function"
    let failProps(key: string) = failwith $"'{key}' field in `props` record is not of Prop<'T> type"
    let [<Literal>] CLASS_EXPR =
        """class extends $0 {
            constructor() {
                super($1...);
            }
        }"""

open LitElementUtil

type PropConfig =
    abstract ``type``: string with get, set
    abstract attribute: U2<string, bool> with get, set

type Prop internal (defaultValue: obj, options: obj) =
    member internal _.ToConfig() = defaultValue, options

    // Using static member instead of constructor in case we need to inline later
    // (e.g. to get the type of an empty array)
    static member Of(defaultValue: 'T, ?attribute: string) =
        let options = jsOptions<PropConfig>(fun o ->
            let typ =
                match box defaultValue with
                | :? string -> Some "String"
                | :? int | :? float -> Some "Number"
                // TODO: I'm having issues with boolean attributes,
                // maybe they're faulty in the current Lit 2 RC?
                | :? bool -> Some "Boolean"
                // TODO: JSON-serializable arrays and objects
                | _ -> None

            typ |> Option.iter (fun v -> o.``type`` <- v)
            attribute |> Option.iter (fun att ->
                match att.Trim() with
                // Let's use empty string to sign no attribute,
                // although we may need to be more explicit later
                | null | "" -> o.attribute <- !^false
                | att -> o.attribute <- !^att)
        )
        Prop<'T>(defaultValue, options)

and Prop<'T> internal (defaultValue: 'T, options: obj) =
    inherit Prop(defaultValue, options)
    [<Emit("$0")>] member _.Value = defaultValue

type LitConfig<'Props> =
    abstract props: 'Props with get, set
    abstract styles: Styles list with get, set

type ILitElementInit<'Props> =
    abstract init: initFn: (LitConfig<'Props> -> unit) -> 'Props

type LitElementInit<'Props>() =
    let mutable _initialized = false
    let mutable _props = Unchecked.defaultof<'Props>
    let mutable _styles = Unchecked.defaultof<Styles list>

    member _.Initialized = _initialized

    interface LitConfig<'Props> with
        member _.props with get() = _props and set v = _props <- v
        member _.styles with get() = _styles and set v = _styles <- v

    interface ILitElementInit<'Props> with
        member this.init initFn =
            _initialized <- true
            initFn this
            Unchecked.defaultof<'Props>

    interface IHookProvider with
        member _.useState(init) = failInit()
        member _.useRef(init) = failInit()
        member _.useEffect(effect) = failInit()
        member _.useEffectOnce(effect) = failInit()
        member _.useElmish(init, update) = failInit()

[<AttachMembers>]
type LitHookElement<'Props>(renderFn: JS.Function, ?initProps: JS.Function) =
    inherit LitElementBase()
    do initProps |> Option.iter(fun f -> f.Invoke([|jsThis|]) |> ignore)
    let provider =
        HookProvider(
            emitJsExpr renderFn "() => $0.apply(this)",
            emitJsExpr () "() => this.requestUpdate()",
            emitJsExpr () "() => this.isConnected")

    member _.render() =
        provider.render()

    member _.disconnectedCallback() =
        base.disconnectedCallback()
        provider.disconnect()

    member _.connectedCallback() =
        base.connectedCallback()
        provider.runEffects (onConnected = true, onRender = false)

    interface ILitElementInit<'Props> with
        member this.init(_): 'Props = box this :?> 'Props

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
        if renderFn?length > 0 then
            failwith "Render function for LitElement cannot take arguments"

        let config = LitElementInit()
        try
            renderFn.apply(config, [||]) |> ignore
        with _ -> ()

        if not config.Initialized then
            failInit()

        let config = config :> LitConfig<obj>
        let baseClass = jsConstructor<LitHookElement<obj>>

        let classExpr =
            if isDefined config.props then
                let propsValues = ResizeArray()
                let propsOptions = obj()

                (JS.Constructors.Object.keys(config.props),
                 JS.Constructors.Object.values(config.props))
                ||> Seq.zip
                |> Seq.iter (fun (k, v) ->
                    let defVal, options =
                        match box v with
                        | :? Prop as v -> v.ToConfig()
                        // We could return `v, obj()` here but let's make devs used to
                        // initialize Props, which should make the code more consistent
                        | _ -> failProps(k)
                    propsValues.Add(k, defVal)
                    propsOptions?(k) <- options)

                let initProps (this: obj) =
                    propsValues |> Seq.iter(fun (k, v) ->
                        this?(k) <- v)

                let classExpr = emitJsExpr (baseClass, renderFn, initProps) CLASS_EXPR
                this.defineGetter(classExpr, "properties", fun () -> propsOptions)
                classExpr
            else
                emitJsExpr (baseClass, renderFn) CLASS_EXPR

        if isDefined config.styles then
            this.defineGetter(classExpr, "styles", fun () -> List.toArray config.styles)

        // Register custom element
        this.defineCustomElement(name, classExpr)
        box(fun () -> failwith $"{name} is not immediately callable, it must be created in HTML") :?> _

type LitElement =
    static member inline init() =
        jsThis<ILitElementInit<unit>>.init(fun _ -> ())

    static member inline init(initFn: LitConfig<'Props> -> unit) =
        jsThis<ILitElementInit<'Props>>.init(initFn)