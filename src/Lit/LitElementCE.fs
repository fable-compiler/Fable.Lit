namespace Lit

open Fable.Core
open Fable.Core.JsInterop
open Browser.Dom

[<AutoOpen>]
module LitElementCE =

    type ConverterBase =
        interface
        end

    type ConverterFrom =
        inherit ConverterBase
        abstract fromAttribute: JS.Function with get, set

    type ConverterTo =
        inherit ConverterBase
        abstract toAttribute: JS.Function with get, set

    type Converter =
        inherit ConverterFrom
        inherit ConverterTo

    type ReactivePropertyData =
        | Type of PropType
        | NamedAttribute of string
        | State
        | Attribute
        | Reflect
        | NoAccessor
        | Converter of ConverterBase
        | HasChanged of JS.Function

    type PropBuilderResult =
        | PropBuilderResult of obj

        member this.Value =
            match this with
            | PropBuilderResult value -> value

    type ReactiveProperty = string * PropBuilderResult

    type LitStaticConfiguration =
        | Style of CSSResult
        | Property of ReactiveProperty


    type ElementDefinition =
        { name: string
          elementConstructor: obj
          props: ReactiveProperty list
          styles: CSSResult list }


    type PropBuilder(propName: string) =

        member inline _.Yield _ = List.empty<ReactivePropertyData>

        /// <summary>
        /// Indicates the type of the property. This is used only as a hint for the converter to determine how to convert the attribute to/from a property.
        /// </summary>
        [<CustomOperation "use_type">]
        member inline _.AddType(state: ReactivePropertyData list, typeCtor: PropType) = Type typeCtor :: state

        /// <summary>
        ///Indicates the property is internal private state.
        /// The property should not be set by users.
        /// This property should be marked as private (in F# let declarations inside classes are private by default)
        /// The property is not added to observedAttributes.
        /// </summary>
        [<CustomOperation "use_state">]
        member inline _.AddState(state: ReactivePropertyData list) = State :: state

        /// <summary>
        /// Indicates how and whether the property becomes an observed attribute.
        /// The string value is observed (e.g attribute: 'foo-bar').
        /// </summary>
        [<CustomOperation "use_named_attribute">]
        member inline _.AddAttribute(state: ReactivePropertyData list, attributeName: string) =
            NamedAttribute attributeName :: state

        /// <summary>
        /// Indicates how and whether the property becomes an observed attribute.
        /// If true or absent, the lowercased property name is observed (e.g. fooBar becomes foobar)
        /// </summary>
        [<CustomOperation "use_attribute">]
        member inline _.AddAttribute(state: ReactivePropertyData list) = Attribute :: state


        /// <summary>
        /// Indicates if the property should reflect to an attribute.
        /// When the property is set, the attribute is set using the attribute
        /// name determined according to the rules for the use_attribute/use_named_attribute
        /// and the value of the property converted using the rules from the use_converter option.
        /// </summary>
        [<CustomOperation "use_reflect">]
        member inline _.AddReflect(state: ReactivePropertyData list) = Reflect :: state

        /// <summary>
        /// Indicates whether an accessor will not be created for this property.
        /// By default, an accessor will be generated for this property that requests an update when set.
        /// no accessor will be created, and it will be the user's responsibility to call this.requestUpdate(propertyName, oldValue)
        /// to request an update when the property changes.
        /// </summary>
        [<CustomOperation "use_no_accessor">]
        member inline _.AddNoAccessor(state: ReactivePropertyData list) = NoAccessor :: state


        /// <summary>
        /// Indicates how to convert the attribute to/from a property.
        /// A default converter is used if none is provided; it supports Boolean, String, Number, Object, and Array.
        /// Note, when a property changes and the converter is used to update the attribute,
        /// the property is never updated again as a result of the attribute changing, and vice versa.
        /// </summary>
        [<CustomOperation "use_converter">]
        member inline _.AddConverter(state: ReactivePropertyData list, converter: Converter) =
            Converter converter :: state

        /// <summary>
        /// A function that indicates if a property should be considered changed when it is set.
        /// The function should take the newValue and oldValue and return true if an update should be requested.
        /// </summary>
        [<CustomOperation "use_has_changed">]
        member inline _.AddHasChanged(state: ReactivePropertyData list, hasChanged: JS.Function) =
            HasChanged hasChanged :: state

        member _.Run state : ReactiveProperty =
            let opts =
                [ for value in state do
                      match value with
                      | Type value -> "type", value :> obj
                      | NamedAttribute value -> "attribute", value :> obj
                      | State -> "state", true :> obj
                      | Attribute -> "attribute", true :> obj
                      | Reflect -> "reflect", true :> obj
                      | NoAccessor -> "noAccessor", true :> obj
                      | Converter value -> "converter", value :> obj
                      | HasChanged value -> "hasChanged", value :> obj ]
                |> createObj

            propName, PropBuilderResult opts

    let inline defineElement (definition: ElementDefinition) =
        definition.elementConstructor?styles <- definition.styles |> List.toArray

        definition.elementConstructor?properties <-
            [ for name, prop in definition.props do
                  name, prop.Value ]
            |> createObj

        match
            window?customElements?get (definition.name)
            |> Option.ofObj
            with
        | None -> window?customElements?define (definition.name, definition.elementConstructor)
        | Some _ -> JS.console.warn ($"Element {definition.name} has already been defined, skipping.")

    let inline private stateToElementDef name elementConstructor (state: LitStaticConfiguration list) =
        let styles = ResizeArray()
        let props = ResizeArray()

        for s in state do
            match s with
            | Style value -> styles.Add value
            | Property value -> props.Add value

        { name = name
          elementConstructor = elementConstructor
          props = props |> List.ofSeq
          styles = styles |> List.ofSeq }

    type LitElementBuilder(name, elementConstructor) =

        member inline _.Yield(property: ReactiveProperty) = [ Property property ]
        member inline _.Yield(style: CSSResult) = [ Style style ]

        member inline _.Combine(c1: (LitStaticConfiguration) list, c2: (LitStaticConfiguration) list) = c1 @ c2

        member inline _.Delay(delay) = delay ()

        member inline _.Zero() = ()

        member _.Run(state: LitStaticConfiguration list) =
            stateToElementDef name elementConstructor state

    type LitElementBuilderAuto(name, elementConstructor) =

        member inline _.Yield(property: ReactiveProperty) = [ Property property ]
        member inline _.Yield(style: CSSResult) = [ Style style ]

        member inline _.Combine(c1: LitStaticConfiguration list, c2: LitStaticConfiguration list) = c1 @ c2

        member inline _.Delay(delay) = delay ()

        member inline _.Zero() = ()

        member _.Run state =
            stateToElementDef name elementConstructor state
            |> defineElement

    /// <summary>
    /// Takes the name of the property, and allows you to configure it similar to the `static get properties() {}` from lit.
    /// This string name has to match with an existing private field in a class component
    /// </summary>
    /// <example>
    /// <code lang="fsharp">
    /// type MyElement() =
    ///     inherit LitElement()
    ///
    ///     let mutable counter = 0
    ///
    ///     override _.render() = html $"..."
    ///
    /// registerElement&lt;MyElement> "my-element" {
    ///     reactiveProperty "counter" {
    ///         use_state
    ///     }
    /// }
    /// </code>
    /// </example>
    let reactiveProperty propName = PropBuilder(propName)

    /// <summary>
    /// Builds a lit element ready to be registered with the custom elements registry
    /// use this when you want to control when to register an element
    /// </summary>
    /// <example>
    /// <code lang="fsharp">
    /// let myElDefinition =
    ///     delayedElement&lt;MyElement> "my-element" {
    ///         reactiveProperty "counter" {
    ///             use_state
    ///         }
    ///     }
    /// // later in the code
    /// let register() =
    ///     defineElement myElDefinition
    /// </code>
    /// </example>
    let inline delayedElement<'T> name =
        LitElementBuilder(name, jsConstructor<'T>)

    /// <summary>
    /// The simplest way to register an element and provide no configuration
    /// if for some reason you don't need to declare properties or css for custom element
    /// then you can use this function, but function
    /// </summary>
    /// <example>
    /// <code lang="fsharp">
    /// registerElementNoConfig&lt;MyElement> "my-element"
    /// </code>
    /// </example>
    let inline registerElementNoConfig<'T> name =
        LitElementBuilderAuto(name, jsConstructor<'T>)
            .Run []

    /// <summary>
    /// Builds a lit element and automatically registers it with the custom elements registry
    /// use this when you don't care about controlling the custom element's registration
    /// </summary>
    /// <example>
    /// <code lang="fsharp">
    /// registerElement&lt;MyElement> "my-element" {
    ///     css $"p {{ color: red; }}"
    ///     css $"li {{ color: rebeccapurple; }}"
    ///     reactiveProperty "counter" {
    ///       use_state
    ///    }
    /// }
    /// </code>
    /// </example>
    let inline registerElement<'T> name =
        LitElementBuilderAuto(name, jsConstructor<'T>)
