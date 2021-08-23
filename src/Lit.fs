module Lit

open Fable.Core.JsInterop
open Browser.Types
open Fable.Core

type TemplateResult =
    interface end

module Bindings =
    let html: Template.JsTag<TemplateResult> = importMember "lit-html"
    let svg: Template.JsTag<TemplateResult> = importMember "lit-html"
    let render (t: TemplateResult) (el: HTMLElement): unit = importMember "lit-html"
    let styleMap (styles: obj): obj = importMember "lit-html/directives/style-map"

    let classMap (classes: obj): obj = importMember "lit-html/directives/class-map"
    let private _until: obj = import "until" "lit-html/directives/until"

    [<Emit("Bindings__until(...$0)")>]
    let until (values: obj[]): TemplateResult = jsNative


let html: Template.Tag<_> = Template.transform Bindings.html

/// svg is required for nested templates within an svg element
let svg: Template.Tag<_> = Template.transform Bindings.svg

let render el t = Bindings.render t el

/// Equivalent to lit-hmtl styleMap, accepting a list of Feliz styles
let styles (styles: Feliz.CssStyle seq) =
    let map = obj()
    styles
    |> Seq.iter (fun (Feliz.CssStyle(key, value)) ->
        map?(key) <- value)
    Bindings.styleMap map
    
let classes (classes: (string * bool) seq) = 
    Bindings.classMap (keyValueList CaseRules.LowerFirst classes)

let repeat<'T> (items: 'T seq) (template: 'T -> int -> TemplateResult) = importMember "lit-html/directives/repeat"

let cache (template: TemplateResult): TemplateResult = importMember "lit-html/directives/cache"

let guard (deps: obj[]) (render: unit -> TemplateResult): TemplateResult = importMember "lit-html/directives/guard"

let until (values: obj[]) (placeholder: TemplateResult) =
    let result = [| yield! values; placeholder |]
    Bindings.until result