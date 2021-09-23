namespace Lit

open Fable.Core
open Fable.Core.JsInterop

[<AutoOpen>]
module LitElementExtensions =
    type Browser.Types.HTMLElement with
        [<Emit("$0.updateComplete")>]
        /// Returns a promise that will resolve when the element has finished updating.
        /// Only accessible in LitElements.
        member _.updateComplete: JS.Promise<unit> = jsNative

module private LitElementUtil =
    module Types =
        let [<Global>] String = obj()
        let [<Global>] Number = obj()
        let [<Global>] Boolean = obj()
        let [<Global>] Array = obj()
        let [<Global>] Object = obj()

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

type Converter =
    abstract fromAttribute: JS.Function with get, set
    abstract toAttribute: JS.Function with get, set

type PropConfig =
    /// <summary>
    /// JS Constructor to help with value change detection and comparison.
    /// Indicates the type of the property. This is used only as a hint for the
    /// `converter` to determine how to convert the attribute to/from a property.
    /// `Boolean`, `String`, `Number`, `Object`, and `Array` should be used.
    /// </summary>
    abstract ``type``: obj with get, set
    /// <summary>
    /// Indicates the property becomes an observed attribute.
    /// </summary>
    /// <remarks>The value has to be lower-cased and dash-cased due to the HTML Spec.</remarks>
    abstract attribute: U2<string, bool> with get, set
    /// <summary>
    /// Indicates the property is internal private state. The property should not be set by users.
    /// A common practice is to use a leading `_` in the name.
    /// The property is not added to `observedAttributes`.
    /// </summary>
    abstract state: bool with get, set
    /// <summary>
    /// Indicates if the property should reflect to an attribute.
    /// If `true`, when the property is set, the attribute is set using the
    /// attribute name determined according to the rules for the `attribute`
    /// property option and the value of the property converted using the rules
    /// from the `converter` property option.
    /// </summary>
    abstract reflect: bool with get, set
    /// <summary>
    /// Indicates whether an accessor will be created for this property. By
    /// default, an accessor will be generated for this property that requests an
    /// update when set. No accessor will be created, and
    /// it will be the user's responsibility to call
    /// `this.requestUpdate(propertyName, oldValue)` to request an update when
    /// the property changes.
    /// </summary>
    abstract noAccessor: bool with get, set
    /// <summary>
    /// Indicates how to convert the attribute to/from a property.
    /// If this value is a function, it is used to convert the attribute value a the property value.
    /// If it's an object, it can have keys for fromAttribute and toAttribute.
    /// If no toAttribute function is provided and reflect is set to true,
    /// the property value is set directly to the attribute. A default converter is used if none is provided;
    /// it supports Boolean, String, Number, Object, and Array. Note, when a property changes
    /// and the converter is used to update the attribute,
    /// the property is never updated again as a result of the attribute changing, and vice versa.
    /// </summary>
    abstract converter: Converter with get, set

    abstract hasChanged: JS.Function with get, set

type Prop internal (defaultValue: obj, options: obj) =
    member internal _.ToConfig() = defaultValue, options

    // Using static member instead of constructor in case we need to inline later
    // (e.g. to get the type of an empty array)

    /// <summary>
    /// Creates a property accessor.
    /// </summary>
    /// <param name="defaultValue">The initialization value.</param>
    /// <param name="attribute">Custom name of the HTML attribute (e.g. "my-prop"). Pass an empty string to disable exposing an HTML attribute. [More info](https://lit.dev/docs/components/properties/#observed-attributes).</param>
    /// <param name="hasChanged">Custom value change detection. [More info](https://lit.dev/docs/components/properties/#haschanged)</param>
    /// <param name="fromAttribute">Convert from the string attribute to the typed property. [More info](https://lit.dev/docs/components/properties/#conversion-converter).</param>
    /// <param name="toAttribute">Convert from the typed property to the string attribute. [More info](https://lit.dev/docs/components/properties/#conversion-converter).</param>
    /// <param name="reflect">When the property changes, reflect its value back to the HTML attribute (default: false). [More info](https://lit.dev/docs/components/properties/#reflected-attributes).</param>
    /// <returns>The property accessor.</returns>
    static member Of
        (
            defaultValue: 'T,
            ?attribute: string,
            ?hasChanged: 'T -> 'T -> bool,
            ?fromAttribute: string -> 'T,
            ?toAttribute: 'T -> string,
            ?reflect: bool
        ) =
        let options = jsOptions<PropConfig>(fun o ->
            let typ =
                match box defaultValue with
                | :? string -> Some Types.String
                | :? int | :? float -> Some Types.Number
                | :? bool -> Some Types.Boolean
                // TODO: Detect if it's an array of primitives or a record and use Array/Object
                | _ -> None
            typ |> Option.iter (fun v -> o.``type`` <- v)
            reflect |> Option.iter (fun v -> o.reflect <- v)
            hasChanged |> Option.iter (fun v -> o.hasChanged <- unbox v)
            attribute |> Option.iter (fun att ->
                match att.Trim() with
                // Let's use empty string to sign no attribute,
                // although we may need to be more explicit later
                | null | "" -> o.attribute <- !^false
                | att -> o.attribute <- !^att)
            match fromAttribute, toAttribute with
            | Some _, _ | _, Some _ ->
                o.converter <- jsOptions<Converter>(fun o ->
                    fromAttribute |> Option.iter (fun v -> o.fromAttribute <- unbox v)
                    toAttribute |> Option.iter (fun v -> o.toAttribute <- unbox v)
                )
            | _ -> ()
        )
        Prop<'T>(defaultValue, options)

and Prop<'T> internal (defaultValue: 'T, options: obj) =
    inherit Prop(defaultValue, options)
    [<Emit("$0{{ = $1}}")>]
    member _.Value with get() = defaultValue and set(_: 'T) = ()

type LitConfig<'Props> =
    abstract props: 'Props with get, set
    abstract styles: Styles list with get, set

type ILitElementInit<'Props> =
    abstract init: initFn: (LitConfig<'Props> -> unit) -> LitElement * 'Props

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
            Unchecked.defaultof<_>

    interface IHookProvider with
        member _.hooks = failInit()

[<AttachMembers>]
type LitHookElement<'Props>(renderFn: JS.Function, ?initProps: JS.Function) =
    inherit LitElement()
    do initProps |> Option.iter(fun f -> f.Invoke([|jsThis|]) |> ignore)
    let context =
        HookContext(
            emitJsExpr renderFn "() => $0.apply(this)",
            emitJsExpr () "() => this.requestUpdate()",
            emitJsExpr () "() => this.isConnected")

    member _.render() =
        context.render()

    member _.disconnectedCallback() =
        base.disconnectedCallback()
        context.disconnect()

    member _.connectedCallback() =
        base.connectedCallback()
        context.runEffects (onConnected = true, onRender = false)

    interface ILitElementInit<'Props> with
        member this.init(_) = this :> LitElement, box this :?> 'Props

    interface IHookProvider with
        member _.hooks = context

type LitElementAttribute(name: string) =
    inherit JS.DecoratorAttribute()

    [<Emit("customElements.define($1, $2)")>]
    member _.defineCustomElement (name: string, cons: obj) = ()

    [<Emit("Object.defineProperty($1, $2, { get: $3 })")>]
    member _.defineGetter(target: obj, name: string, f: unit -> 'V) = ()

    override this.Decorate(renderFn) =
        if renderFn.length > 0 then
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

[<AutoOpen>]
module LitElementExt =
    open Browser
    open Browser.Types

    // TODO: Not sure why we don't have this constructor in Fable.Browser, we should add it
    // CustomEvent constructor is also transpiled incorrectly, not sure why
    [<Emit("new Event($0, $1)")>]
    let private createEvent (name: string) (opts: EventInit): Event = jsNative

    [<Emit("new CustomEvent($0, $1)")>]
    let private createCustomEvent (name: string) (opts: CustomEventInit<'T>): CustomEvent<'T> = jsNative

    type LitElement with
        member this.dispatchEvent(name: string, ?bubbles: bool, ?composed: bool, ?cancelable: bool): bool =
            jsOptions<EventInit>(fun o ->
                o.bubbles <- defaultArg bubbles true
                o.composed <- defaultArg composed true
                o.cancelable <- defaultArg cancelable true
            )
            |> createEvent name
            |> this.el.dispatchEvent

        member this.dispatchCustomEvent(name: string, ?detail: 'T, ?bubbles: bool, ?composed: bool, ?cancelable: bool): bool =
            jsOptions<CustomEventInit<'T>>(fun o ->
                // Be careful if `detail` is not option, Fable may wrap it with `Some()`
                // as it's a generic and o.detail expects an option
                o.detail <- detail
                o.bubbles <- defaultArg bubbles true
                o.composed <- defaultArg composed true
                o.cancelable <- defaultArg cancelable true
            )
            |> createCustomEvent name
            |> this.el.dispatchEvent

        static member inline init() =
            jsThis<ILitElementInit<unit>>.init(fun _ -> ())

        static member inline init(initFn: LitConfig<'Props> -> unit) =
            jsThis<ILitElementInit<'Props>>.init(initFn)
