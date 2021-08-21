module Lit

open Fable.Core.JsInterop
open Browser.Types

type TemplateResult =
    interface end

module Bindings =
    let html: Template.JsTag<TemplateResult> = importMember "lit-html"
    let svg: Template.JsTag<TemplateResult> = importMember "lit-html"
    let render (t: TemplateResult) (el: HTMLElement): unit = importMember "lit-html"
    let styleMap (styles: obj): obj = importMember "lit-html/directives/style-map"

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
