namespace Lit

open System
open Browser.Types
open Fable.Core

type TemplateResult =
    interface end

type RefValue<'T> =
    abstract value: 'T option with get, set

[<ImportMember("lit-html/directive.js")>]
type Directive() =
    class end

[<ImportMember("lit-html/async-directive.js")>]
type AsyncDirective() =
    member _.setValue(value: obj): unit = jsNative

type Part =
    interface end

type ChildPart =
    inherit Part
    abstract parentNode: Element

type LitHtml =
    [<ImportMember("lit-html")>]
    static member html: Template.JsTag<TemplateResult> = jsNative

    [<ImportMember("lit-html")>]
    static member svg: Template.JsTag<TemplateResult> = jsNative

    [<ImportMember("lit-html")>]
    static member render (t: TemplateResult, el: Element): unit = jsNative

    [<ImportMember("lit-html")>]
    static member nothing: TemplateResult = jsNative

    [<ImportMember("lit-html")>]
    static member noChange: TemplateResult = jsNative

    [<ImportMember("lit-html/directive.js")>]
    static member directive (cons: obj): obj = jsNative

    [<ImportMember("lit-html/directives/style-map.js")>]
    static member styleMap (styles: obj): obj = jsNative

    [<ImportMember("lit-html/directives/class-map.js")>]
    static member classMap (classes: obj): obj = jsNative

    [<ImportMember("lit-html/directives/until.js")>]
    static member until ([<ParamArray>] values: obj[]): TemplateResult = jsNative

    [<ImportMember("lit-html/directives/repeat.js")>]
    static member repeat<'T> (items: 'T seq, getId: 'T -> string, template: 'T -> int -> TemplateResult) = jsNative

    [<ImportMember("lit-html/directives/cache.js")>]
    static member cache (template: TemplateResult): TemplateResult = jsNative

    [<ImportMember("lit-html/directives/guard.js")>]
    static member guard (deps: obj[], render: unit -> TemplateResult): TemplateResult = jsNative

    [<ImportMember("lit-html/directives/if-defined.js")>]
    static member ifDefined(value: obj): TemplateResult = jsNative

    [<ImportMember("lit-html/directives/ref.js")>]
    static member ref(refOrCallback: obj): TemplateResult = jsNative

    [<ImportMember("lit-html/directives/ref.js")>]
    static member createRef<'T>(): RefValue<'T> = jsNative

type Lit() =
    static let _html: Template.Tag<_> = Template.transform LitHtml.html
    static let _svg: Template.Tag<_> = Template.transform LitHtml.svg

    static member html = _html

    /// svg is required for nested templates within an svg element
    static member svg = _svg

    static member nothing = LitHtml.nothing

    static member render el t = LitHtml.render(t, el)

    static member classes (classes: (string * bool) seq) =
        // LitHtml.classMap (keyValueList CaseRules.LowerFirst classes)
        classes |> Seq.choose (fun (s, b) -> if b then Some s else None) |> String.concat " "

    static member classes (classes: string seq) =
        classes |> String.concat " "

    /// Use memoize only when the template argument can change considerable depending on some condition (e.g. a modal or nothing).
    static member memoize (template: TemplateResult): TemplateResult =
        LitHtml.cache template

    static member ofSeq (items: TemplateResult seq): TemplateResult = unbox items

    static member ofList (items: TemplateResult list): TemplateResult = unbox items

    /// Give a unique id to items in a list. This can improve performance in lists that will be sorted or filtered.
    static member mapUnique (getId: 'T -> string) (template: 'T -> TemplateResult) (items: 'T seq) =
        LitHtml.repeat(items, getId, fun x _ -> template x)

    /// The view function will only be re-run if one of the dependencies change
    static member ofLazy (dependencies: obj list) (view: unit -> TemplateResult): TemplateResult =
        // TODO: Should we try to use F# equality here?
        LitHtml.guard(List.toArray dependencies, view)

    /// Shows the placeholder until the promise is resolved
    static member ofPromise (placeholder: TemplateResult) (deferred: JS.Promise<TemplateResult>) =
        LitHtml.until(deferred, placeholder)

    static member ofStr (v: string): TemplateResult = unbox v

    static member ofText (v: string): TemplateResult = unbox v

    static member ofInt (v: int): TemplateResult = unbox v

    static member ofFloat (v: float): TemplateResult = unbox v

    /// Sets the attribute if the value is defined and removes the attribute if the value is undefined.
    static member attrOfOption (attributeValue: string option) =
        LitHtml.ifDefined attributeValue

    static member inline createRef<'T>(): RefValue<'T> =
        LitHtml.createRef<'T>()

    static member inline refValue<'El when 'El :> Element> (v: RefValue<'El>) =
        LitHtml.ref v

    static member inline refFn<'El when 'El :> Element> (fn: 'El option -> unit) =
        LitHtml.ref fn

    static member inline directive<'Class, 'Arg>(): 'Arg -> TemplateResult =
        LitHtml.directive JsInterop.jsConstructor<'Class> :?> _
