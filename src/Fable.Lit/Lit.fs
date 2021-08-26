module Lit

open System
open Fable.Core.JsInterop
open Browser.Types
open Fable.Core

type TemplateResult =
    interface end

type RefValue<'T> =
    abstract value: 'T option

type LitHtml =
    [<ImportMember("lit-html")>]
    static member html: Template.JsTag<TemplateResult> = jsNative

    [<ImportMember("lit-html")>]
    static member svg: Template.JsTag<TemplateResult> = jsNative

    [<ImportMember("lit-html")>]
    static member render (t: TemplateResult, el: HTMLElement): unit = jsNative

    [<ImportMember("lit-html")>]
    static member nothing: TemplateResult = jsNative

    [<ImportMember("lit-html/directives/style-map")>]
    static member styleMap (styles: obj): obj = jsNative

    [<ImportMember("lit-html/directives/class-map")>]
    static member classMap (classes: obj): obj = jsNative

    [<ImportMember("lit-html/directives/until")>]
    static member until ([<ParamArray>] values: obj[]): TemplateResult = jsNative

    [<ImportMember("lit-html/directives/repeat")>]
    static member repeat<'T> (items: 'T seq, getId: 'T -> string, template: 'T -> int -> TemplateResult) = jsNative

    [<ImportMember("lit-html/directives/cache")>]
    static member cache (template: TemplateResult): TemplateResult = jsNative

    [<ImportMember("lit-html/directives/guard")>]
    static member guard (deps: obj[], render: unit -> TemplateResult): TemplateResult = jsNative

    [<ImportMember("lit-html/directives/if-defined")>]
    static member ifDefined(value: obj): TemplateResult = jsNative

    [<ImportMember("lit-html/directives/ref")>]
    static member ref(refOrCallback: obj): TemplateResult = jsNative

    [<ImportMember("lit-html/directives/ref")>]
    static member createRef<'T>(): RefValue<'T> = jsNative

let html: Template.Tag<_> = Template.transform LitHtml.html

/// svg is required for nested templates within an svg element
let svg: Template.Tag<_> = Template.transform LitHtml.svg

let nothing = LitHtml.nothing

let render el t = LitHtml.render(t, el)

let classes (classes: (string * bool) seq) =
    // LitHtml.classMap (keyValueList CaseRules.LowerFirst classes)
    classes |> Seq.choose (fun (s, b) -> if b then Some s else None) |> String.concat " "

/// Use memoize only when the template argument can change considerable depending on some condition (e.g. a modal or nothing).
let memoize (template: TemplateResult): TemplateResult =
    LitHtml.cache template

/// Use repeat to give a unique id to items in a list. This can improve performance in lists that will be sorted or filtered.
let ofSeqWithId (getId: 'T -> string) (template: 'T -> TemplateResult) (items: 'T seq) =
    LitHtml.repeat(items, getId, fun x _ -> template x)

/// The view function will only be re-run if one of the dependencies change
let ofLazy (dependencies: obj list) (view: unit -> TemplateResult): TemplateResult =
    // TODO: Should we try to use F# equality here?
    LitHtml.guard(List.toArray dependencies, view)

/// Shows the placeholder until the promise is resolved
let ofPromise (placeholder: TemplateResult) (deferred: JS.Promise<TemplateResult>) =
    LitHtml.until(deferred, placeholder)

/// Sets the attribute if the value is defined and removes the attribute if the value is undefined.
let ifSome (attributeValue: string option) =
    LitHtml.ifDefined attributeValue

let createRef<'T>(): RefValue<'T> =
    LitHtml.createRef<'T>()

let refValue<'El when 'El :> Element> (v: RefValue<'El>) =
    LitHtml.ref v

let refFn<'El when 'El :> Element> (fn: 'El option -> unit) =
    LitHtml.ref fn
