module Lit

open System
open Fable.Core.JsInterop
open Browser.Types
open Fable.Core

type TemplateResult =
    interface end

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

let html: Template.Tag<_> = Template.transform LitHtml.html

/// svg is required for nested templates within an svg element
let svg: Template.Tag<_> = Template.transform LitHtml.svg

let render el t = LitHtml.render(t, el)

/// Equivalent to lit-hmtl styleMap, accepting a list of Feliz styles
let styles (styles: Feliz.CssStyle seq) =
    let map = obj()
    styles
    |> Seq.iter (fun (Feliz.CssStyle(key, value)) ->
        map?(key) <- value)
    LitHtml.styleMap map
    
let classes (classes: (string * bool) seq) = 
    // LitHtml.classMap (keyValueList CaseRules.LowerFirst classes)
    classes |> Seq.choose (fun (s, b) -> if b then Some s else None) |> String.concat " "

/// Use repeat to give a unique id to items in a list. This can improve performance in lists that will be sorted or filtered.
let repeat (getId: 'T -> string) (template: 'T -> TemplateResult) (items: 'T seq) =
    LitHtml.repeat(items, getId, fun x _ -> template x)

/// Caches the rendered DOM nodes for templates when they're not in use. The conditionalTemplate argument is an expression that can return one of several templates. cache renders the current value of conditionalTemplate. When the template changes, the directive caches the current DOM nodes before switching to the new value.
///
/// When lit-html re-renders a template, it only updates the modified portions: it doesn't create or remove any more DOM than it needs to. But when you switch from one template to another, lit-html needs to remove the old DOM and render a new DOM tree.
///
/// The cache directive caches the generated DOM for a given binding and input template. In the example above, it would cache the DOM for both the summaryView and detailView templates. When you switch from one view to another, lit-html just needs to swap in the cached version of the new view, and update it with the latest data.
let cache (conditionalTemplate: TemplateResult): TemplateResult =
    LitHtml.cache conditionalTemplate

/// Renders the value returned by valueFn. Only re-evaluates valueFn when one of the dependencies changes identity. Where
/// - dependencies is an array of values to monitor for changes. (For backwards compatibility, dependencies can be a single, non-array value.)
/// - valueFn is a function that returns a renderable value.
/// 
/// `guard` is useful with immutable data patterns, by preventing expensive work until data updates.
let guard (dependencies: obj list) (valueFn: unit -> TemplateResult): TemplateResult =
    LitHtml.guard(List.toArray dependencies, valueFn)

/// Shows the placeholder until the promise is resolved
let until (placeholder: TemplateResult) (deferred: JS.Promise<TemplateResult>) =
    LitHtml.until(deferred, placeholder)

let nothing = LitHtml.nothing

/// Sets the attribute if the value is defined and removes the attribute if the value is undefined.
let ifSome (attributeValue: string option) =
    LitHtml.ifDefined attributeValue