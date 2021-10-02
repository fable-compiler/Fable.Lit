namespace Lit

open System
open Fable.Core
open Fable.Core.JsInterop
open Browser
open Browser.Types

// LitElement should inherit HTMLElement but HTMLElement
// is still implemented as interface in Fable.Browser
[<Import("LitElement", "lit")>]
type LitElement() =
    /// Access the underlying HTMLElement
    [<Emit("$0")>] member _.el: HTMLElement = jsNative
    member _.isConnected: bool = jsNative
    member _.connectedCallback(): unit = jsNative
    member _.disconnectedCallback(): unit = jsNative
    member _.requestUpdate(): unit = jsNative
    /// Returns a promise that will resolve when the element has finished updating.
    member _.updateComplete: JS.Promise<unit> = jsNative

module private LitElementUtil =
    module Types =
        let [<Global>] String = obj()
        let [<Global>] Number = obj()
        let [<Global>] Boolean = obj()
        let [<Global>] Array = obj()
        let [<Global>] Object = obj()

    let isNotNull (x: obj) = not(isNull x)
    let isNotReferenceEquals (x: obj) (y: obj) = not(obj.ReferenceEquals(x, y))
    let failInit() = failwith "LitElement.init must be called on top of the render function"
    let failProps(key: string) = failwith $"'{key}' field in `props` record is not of Prop<'T> type"

    [<Emit("customElements.define($0, $1)")>]
    let defineCustomElement (name: string, cons: obj) = ()

    [<Emit("Object.defineProperty($0, $1, { get: $2 })")>]
    let defineGetter(target: obj, name: string, f: unit -> 'V) = ()

    [<ImportMember("lit")>]
    let adoptStyles(renderRoot, styles): unit = jsNative

    [<Emit("fetch($0).then(x => x.text())")>]
    let fetchText(url: string): JS.Promise<string> = jsNative

#if DEBUG
    let definedElements = Collections.Generic.HashSet<string>()

    let updateStyleSheets (data: obj) (litEl: LitElement) (newCSSResults: CSSResult[]) =
        if isNotNull litEl.el.shadowRoot && isNotNull litEl.el.shadowRoot.adoptedStyleSheets && isNotNull newCSSResults then
            let oldSheets = litEl.el.shadowRoot.adoptedStyleSheets
            let updatedSheets = HMRUtil.getOrAdd data "updatedSheets" (fun _ -> JS.Constructors.Set.Create())
            if oldSheets.Length = newCSSResults.Length then
                Array.zip oldSheets newCSSResults
                |> Array.iter (fun (oldSheet, newCSSResult) ->
                    let newSheet = newCSSResult.styleSheet
                    if isNotNull newCSSResult.cssText && isNotReferenceEquals oldSheet newSheet && not(updatedSheets.has(newSheet)) then
                        oldSheet.replace(newCSSResult.cssText) |> Promise.start
                        updatedSheets.add(newSheet) |> ignore)
#endif

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

/// Configuration values for the LitElement instances
type LitConfig<'Props> =
    ///<summary>
    /// An object containing the reactive properties definitions for the web components to react to changes
    /// </summary>
    /// <example>
    ///     {| color = Prop.Of("red", attribute = "my-color")
    ///        size = Prop.Of(100, attribute = "size") |}
    /// </example>
    abstract props: 'Props with get, set
    ///<summary>
    /// A list of CSS Styles that will be added to the LitElement's styles static property
    /// </summary>
    /// <example>
    ///     [ css $"""
    ///         :host { display: flex; }
    ///         .p { color: red; }
    ///       """]
    /// </example>
    abstract styles: CSSResult list with get, set
    /// Whether the element should render to shadow or light DOM (defaults to true).
    abstract useShadowDom: bool with get, set

type ILitElementInit<'Props> =
    abstract init: initFn: (LitConfig<'Props> -> JS.Promise<unit>) -> LitElement * 'Props

type LitElementInit<'Props>() =
    let mutable _initPromise: JS.Promise<unit> = null
    let mutable _useShadowDom = true
    let mutable _props = Unchecked.defaultof<'Props>
    let mutable _styles = Unchecked.defaultof<CSSResult list>

    member _.InitPromise = _initPromise

    interface LitConfig<'Props> with
        member _.props with get() = _props and set v = _props <- v
        member _.styles with get() = _styles and set v = _styles <- v
        member _.useShadowDom with get() = _useShadowDom and set v = _useShadowDom <- v

    interface ILitElementInit<'Props> with
        member this.init initFn =
            _initPromise <- initFn this
            Unchecked.defaultof<_>

    interface IHookProvider with
        member _.hooks = failInit()

[<AbstractClass; AttachMembers>]
type LitHookElement<'Props>(initProps: obj -> unit) =
    inherit LitElement()
    let _hooks = HookContext(jsThis)
#if DEBUG
    let mutable _hmrSub: IDisposable option = None
#endif
    do initProps(jsThis)

    abstract renderFn: JS.Function with get, set
    abstract name: string

    member _.render() =
        _hooks.render()

    member _.disconnectedCallback() =
        base.disconnectedCallback()
        _hooks.disconnect()

    member _.connectedCallback() =
        base.connectedCallback()
        _hooks.runEffects (onConnected = true, onRender = false)

#if DEBUG
    interface HMRSubscriber with
        member this.subscribeHmr = Some <| fun token ->
            match _hmrSub with
            | Some _ -> ()
            | None ->
                _hmrSub <-
                    token.Subscribe(fun info ->
                        let updatedModule = info.NewModule
                        let updatedExport = updatedModule?(this.name)
                        this.renderFn <- updatedExport?renderFn
                        updateStyleSheets info.Data this (updatedExport?styles)
                    )
                    |> Some
#endif

    interface ILitElementInit<'Props> with
        member this.init(_) = this :> LitElement, box this :?> 'Props

    interface IHookProvider with
        member _.hooks = _hooks

type LitElementAttribute(name: string) =
#if !DEBUG
    inherit JS.DecoratorAttribute()
    override this.Decorate(renderFn) =
#else
    inherit JS.ReflectedDecoratorAttribute()
    override _.Decorate(renderFn, mi) =
#endif
        let config = LitElementInit()
        let dummyFn() = failwith $"{name} is not immediately callable, it must be created in HTML"
        if renderFn.length > 0 then
            failwith "Render function for LitElement cannot take arguments"
        try
            renderFn.apply(config, [||]) |> ignore
        with _ -> ()

        if isNull config.InitPromise then
            failInit()

        config.InitPromise
        |> Promise.iter (fun _ ->
            let config = config :> LitConfig<obj>

            let styles =
                if isNotNull config.styles then List.toArray config.styles |> Some
                else None

            let propsOptions, initProps =
                if isNotNull config.props then
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
                        propsOptions?(k) <- options
                        if not(isNull defVal) then
                            propsValues.Add(k, defVal))

                    let initProps (this: obj) =
                        propsValues |> Seq.iter(fun (k, v) ->
                            this?(k) <- v)

                    Some propsOptions, initProps
                else
                    None, fun _ -> ()

            let classExpr =
                let baseClass = jsConstructor<LitHookElement<obj>>
#if !DEBUG
                emitJsExpr (baseClass, renderFn, initProps) HookUtil.RENDER_FN_CLASS_EXPR
#else
                let renderRef = LitBindings.createRef()
                renderRef.value <- renderFn
                emitJsExpr (baseClass, renderRef, mi.Name, initProps) HookUtil.HMR_CLASS_EXPR
#endif

            propsOptions |> Option.iter (fun props -> defineGetter(classExpr, "properties", fun () -> props))
            styles |> Option.iter (fun styles -> defineGetter(classExpr, "styles", fun () -> styles))

            if not config.useShadowDom then
                emitJsStatement classExpr """$0.prototype.createRenderRoot = function() {
                    return this;
                }"""

#if !DEBUG
            defineCustomElement(name, classExpr)
#else
            // Build a key to avoid registering the element twice when hot reloading
            // We use the element name, the function name and the property names to minimize the chances of a false negative
            // (if there are two actual duplicated custom elements there should be an error indeed).
            let cacheName =
                match propsOptions with
                | None -> mi.Name + "::" + name
                | Some props -> mi.Name + "::" + name + "::" + (JS.Constructors.Object.keys(props) |> String.concat ", ")

            if not(definedElements.Contains(cacheName)) then
                defineCustomElement(name, classExpr)
                definedElements.Add(cacheName) |> ignore

            // This lets us access the updated render function when accepting new modules in HMR
            dummyFn?renderFn <- renderFn
            styles |> Option.iter (fun styles -> dummyFn?styles <- styles)
#endif
        )
        box dummyFn :?> _

[<AutoOpen>]
module LitElementExt =
    // TODO: Not sure why we don't have this constructor in Fable.Browser, we should add it
    // CustomEvent constructor is also transpiled incorrectly, not sure why
    [<Emit("new Event($0, $1)")>]
    let private createEvent (name: string) (opts: EventInit): Event = jsNative

    [<Emit("new CustomEvent($0, $1)")>]
    let private createCustomEvent (name: string) (opts: CustomEventInit<'T>): CustomEvent<'T> = jsNative

    type LitElement with
        /// <summary>
        /// Creates and Dispatches a Browser `Event`
        /// </summary>
        /// <param name="name">Name of the event to dispatch</param>
        /// <param name="bubbles">Allow the event to go through the bubling phase (default true)</param>
        /// <param name="composed">Allow the event to go through the shadow DOM boundary (default true)</param>
        /// <param name="cancelable">Allow the event to be cancelled (e.g. `event.preventDefault()`) (default true)</param>
        member this.dispatchEvent(name: string, ?bubbles: bool, ?composed: bool, ?cancelable: bool): unit =
            jsOptions<EventInit>(fun o ->
                o.bubbles <- defaultArg bubbles true
                o.composed <- defaultArg composed true
                o.cancelable <- defaultArg cancelable true
            )
            |> createEvent name
            |> this.el.dispatchEvent
            |> ignore

        /// <summary>
        /// Creates and Dispatches a Browser `CustomEvent`
        /// </summary>
        /// <param name="name">Name of the event to dispatch</param>
        /// <param name="detail">An optional value that will be available to any event listener via the `event.detail` property</param>
        /// <param name="bubbles">Allow the event to go through the bubling phase (default true)</param>
        /// <param name="composed">Allow the event to go through the shadow DOM boundary (default true)</param>
        /// <param name="cancelable">Allow the event to be cancelled (e.g. `event.preventDefault()`) (default true)</param>
        member this.dispatchCustomEvent(name: string, ?detail: 'T, ?bubbles: bool, ?composed: bool, ?cancelable: bool): unit =
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
            |> ignore

        /// <summary>
        /// Initializes the LitElement instance and registers the element in the custom elements registry
        /// </summary>
        static member inline init(): LitElement =
            jsThis<ILitElementInit<unit>>.init(fun _ -> Promise.lift ()) |> fst

        /// <summary>
        /// Initializes the LitElement instance, reactive properties and registers the element in the custom elements registry
        /// </summary>
        static member inline init(initFn: LitConfig<'Props> -> unit): LitElement * 'Props =
            jsThis<ILitElementInit<'Props>>.init(initFn >> Promise.lift)

        /// <summary>
        /// Initializes the LitElement instance, reactive properties and registers the element in the custom elements registry
        /// </summary>
        static member inline initAsync(initFn: LitConfig<'Props> -> JS.Promise<unit>): LitElement * 'Props =
            jsThis<ILitElementInit<'Props>>.init(initFn)
