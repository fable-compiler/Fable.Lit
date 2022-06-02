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

        [<CustomOperation "use_type">]
        member inline _.AddType(state: ReactivePropertyData list, typeCtor: PropType) = Type typeCtor :: state

        [<CustomOperation "use_state">]
        member inline _.AddState(state: ReactivePropertyData list) = State :: state

        [<CustomOperation "use_named_attribute">]
        member inline _.AddAttribute(state: ReactivePropertyData list, attributeName: string) =
            NamedAttribute attributeName :: state


        [<CustomOperation "use_attribute">]
        member inline _.AddAttribute(state: ReactivePropertyData list) = Attribute :: state

        [<CustomOperation "use_reflect">]
        member inline _.AddReflect(state: ReactivePropertyData list) = Reflect :: state

        [<CustomOperation "use_no_accessor">]
        member inline _.AddNoAccessor(state: ReactivePropertyData list) = NoAccessor :: state

        [<CustomOperation "use_converter">]
        member inline _.AddConverter(state: ReactivePropertyData list, converter: Converter) =
            Converter converter :: state

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

        window?customElements?define (definition.name, definition.elementConstructor)

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

    let property propName = PropBuilder(propName)

    let inline delayedElement<'T> name =
        LitElementBuilder(name, jsConstructor<'T>)

    let inline registerElement<'T> name =
        LitElementBuilderAuto(name, jsConstructor<'T>)

    let inline delayedFuncElement (name, func) = LitElementBuilder(name, func)
    let inline registerFuncElement (name, func) = LitElementBuilderAuto(name, func)
