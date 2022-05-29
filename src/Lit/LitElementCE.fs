namespace Lit

open Fable.Core
open Fable.Core.JsInterop
open Browser.Dom

[<AutoOpen>]
module LitElementCE =

    type ConverterBase = interface end
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
        | Type of obj
        | NamedAttribute of string
        | State
        | Attribute
        | Reflect
        | NoAccessor
        | Converter of ConverterBase
        | HasChanged of JS.Function

    type PropBuilderResult =
        | PropBuilderResult of obj
        
        member this.Value = match this with PropBuilderResult value -> value
    
    type ReactiveProperty = string * PropBuilderResult
    
    type LitStaticConfiguration = 
        | Styles of CSSResult list
        | Properties of ReactiveProperty list
    

    type ElementDefinition = 
        { name: string; elementConstructor: obj; props: ReactiveProperty list; styles: CSSResult list }

    
    type PropBuilder(propName: string) =
        
        member _.Yield _ = List.empty<ReactivePropertyData>
        
        [<CustomOperation "use_type">]
        member _.AddType(state: ReactivePropertyData list, constructor: obj) =
            Type constructor :: state

        [<CustomOperation "use_state">]
        member _.AddState(state: ReactivePropertyData list) =
            State :: state

        [<CustomOperation "use_named_attribute">]
        member _.AddAttribute(state: ReactivePropertyData list, attributeName: string) =
            NamedAttribute attributeName :: state
        
        
        [<CustomOperation "use_attribute">]
        member _.AddAttribute(state: ReactivePropertyData list) =
            Attribute :: state
        
        [<CustomOperation "use_reflect">]
        member _.AddReflect(state: ReactivePropertyData list) =
            Reflect :: state
            
        [<CustomOperation "use_no_accessor">]
        member _.AddNoAccessor(state: ReactivePropertyData list) =
            NoAccessor :: state
            
        [<CustomOperation "use_converter">]
        member _.AddConverter(state: ReactivePropertyData list, converter: Converter) =
            Converter converter :: state
            
        [<CustomOperation "use_has_changed">]
        member _.AddHasChanged(state: ReactivePropertyData list, hasChanged: JS.Function) =
            HasChanged hasChanged :: state

        member _.Run state : ReactiveProperty =
            let opts = 
                [ for value in state do 
                    match value with 
                    | Type value -> "type", value
                    | NamedAttribute value -> "attribute", value :> obj
                    | State -> "state", true :> obj
                    | Attribute -> "attribute", true :> obj
                    | Reflect -> "reflect", true :> obj
                    | NoAccessor -> "noAccessor", true :> obj
                    | Converter value -> "converter", value :> obj
                    | HasChanged value -> "hasChanged", value :> obj
                ]
                |> JsInterop.createObj
            propName, PropBuilderResult opts

    let property propName = PropBuilder(propName)

    let defineElement (definition: ElementDefinition) =
        definition.elementConstructor?styles <-
            definition.styles |> List.toArray
        definition.elementConstructor?properties <-
            [ for name, prop in definition.props do name, prop.Value ] |> createObj
        window?customElements?define(definition.name, definition.elementConstructor)

    type LitElementBuilder(name, elementConstructor) =

        member _.Yield (property: ReactiveProperty) =
            [ Properties ([property]) ]

        member _.Yield (style: CSSResult) =
            [ Styles ([style]) ]

        member _.Delay(delay) = delay()

        member _.Zero() = ()
        
        member _.Combine (c1: (LitStaticConfiguration) list, c2: (LitStaticConfiguration) list) = c1 @ c2

        member _.For (sequence: #seq<'T>, binder : 'T -> (LitStaticConfiguration) list) =
            [ for s in sequence do 
                yield! binder s ]

        member _.Run state =
            let mutable styles = List.empty<CSSResult>
            let mutable props = List.empty<ReactiveProperty>

            for value in state do 
                match value with 
                | Styles style -> styles <- style @ styles
                | Properties prop -> props <- prop @ props

            { name = name; elementConstructor = elementConstructor; props = props; styles = styles }


    type LitElementBuilderAuto(name, elementConstructor) =

        member _.Yield (property: ReactiveProperty) =
            [ Properties ([property]) ]
        member _.Yield (style: CSSResult) =
            [ Styles ([style]) ]
        
        member _.Delay(delay) = delay()
        
        member _.Zero() = ()

        member _.Combine (c1: LitStaticConfiguration list, c2: LitStaticConfiguration list) =
            c1 @ c2

        member _.For (sequence: #seq<'T>, binder : 'T -> (LitStaticConfiguration) list) =
            [ for s in sequence do 
                yield! binder s ]
        
        member _.Run state =
          let props =
            state 
            |> List.choose(fun s -> match s with Styles _ -> None | Properties props -> Some props)
            |> List.collect id
          let styles =
              state 
              |> List.choose(fun s -> match s with Styles styles -> Some styles | Properties _ -> None)
              |> List.collect id
          { name = name; elementConstructor = elementConstructor; props = props |> List.ofSeq; styles = styles }
          |> defineElement

    let delayedElement name ctor = LitElementBuilder(name, ctor)
    let registerElement name ctor = LitElementBuilderAuto(name, ctor)
