namespace Lit

open System
open Fable.Core
open Fable.Core.JsInterop
open Browser

type Property =
    /// <summary>
    /// The initial value for this property
    /// </summary>
    | InitialValue of obj
    /// <summary>
    /// Indicates the property is internal private state. The
    /// property should not be set by users. A common
    /// practice to use a leading `_` in the name. The property is not added to
    /// `observedAttributes`.
    /// </summary>
    | Internal
    /// <summary>
    /// Indicates the property becomes an observed attribute.
    /// the lowercased property name is observed (e.g. `fooBar` becomes `foobar`).
    /// </summary>
    | Attribute of bool
    /// <summary>
    /// Indicates the property becomes an observed attribute.
    /// the string value is observed (e.g 'color-depth').
    /// </summary>
    /// <remarks>The value has to be lower-cased and dash-cased due to the HTML Spec.</remarks>
    | CustomAttribute of string
    /// Indicates the type of the property. This is used only as a hint for the
    /// `converter` to determine how to convert the attribute
    /// to/from a property.
    /// `Boolean`, `String`, `Number`, `Object`, and `Array` should be used.
    | Type of obj
    /// Indicates if the property should reflect to an attribute.
    /// If `true`, when the property is set, the attribute is set using the
    /// attribute name determined according to the rules for the `attribute`
    /// property option and the value of the property converted using the rules
    /// from the `converter` property option.
    | Reflect
    /// Indicates whether an accessor will be created for this property. By
    /// default, an accessor will be generated for this property that requests an
    /// update when set. No accessor will be created, and
    /// it will be the user's responsibility to call
    /// `this.requestUpdate(propertyName, oldValue)` to request an update when
    /// the property changes.
    | NoAccessor

type Prop<'T>(defaultValue: 'T) =
    member _.DefaultValue = defaultValue
    [<Emit("$0")>]
    member _.Value = defaultValue

type ILitElInit =
    abstract init: ?props: ((string * Property list) list) * ?styles: Styles -> obj

type LitElInitData =
    {| props: ((string * Property list) list) option; styles: Styles option |}

type LitElInit() =
    static member fail() = failwith "LitElement.init must be called on top of the render function"

    member val data: LitElInitData option = None with get, set

    interface ILitElInit with
        member this.init(?props, ?styles) =
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


    member _.getLitProperties (properties: ((string * Property list) list)) =
        createObj
            [ for (property, configuration) in properties do
                    property ==> createObj
                        [ for config in configuration do
                            match config with
                            | Internal -> "state" ==> (true :> obj)
                            | Attribute value -> "attribute" ==> (value :> obj)
                            | CustomAttribute value -> "attribute" ==> (value :> obj)
                            | Type value -> "type" ==> value
                            | Reflect -> "reflect" ==> (true :> obj)
                            | NoAccessor -> "noAccessor" ==> (true :> obj)
                            | InitialValue _ -> () ] ]

    override this.Decorate(renderFn) =
        let init = LitElInit()
        try
            renderFn.apply(init, [||]) |> ignore
        with _ -> ()

        match init.data with
        | None -> LitElInit.fail()
        | Some data ->
            let propsDic =
                data.props
                |> Option.map this.getLitProperties

            let initProps (this: obj) =
                match data.props with 
                | None -> ()
                | Some data ->
                    for (property, configuration) in data do
                        for config in configuration do
                            match config with
                            | InitialValue value ->
                                this?(property) <- value
                            | _ -> ()


            let classExpr =
                emitJsExpr (jsConstructor<LitHookElement>, renderFn, initProps) """class extends $0 {
                    constructor() {
                        super($1, $2);
                    }
                }"""

            match propsDic with
            | None -> ()
            | Some propsDic ->
                this.defineGetter(classExpr, "properties", fun () -> propsDic)

            match data.styles with
            | None -> ()
            | Some styles ->
                this.defineGetter(classExpr, "styles", fun () -> styles)

            // Register custom element
            this.defineCustomElement(name, classExpr)
            box(fun () -> failwith $"{name} is not immediately callable, it must be created in HTML") :?> _

type LitEl =
    static member inline init<'Props>(?props: list<string * list<Property>>, ?styles: Styles) =
        jsThis<ILitElInit>.init(?props = props, ?styles=styles) :?> 'Props