module Feliz.Lit

open Fable.Core
open Fable.Core.JsInterop
open Feliz
open Lit

[<Erase>]
type CssStyle = CssStyle of key: string * value: string

let Css = CssEngine(fun k v -> CssStyle(k, v))

/// Equivalent to lit-hmtl styleMap, accepting a list of Feliz styles
let styles (styles: CssStyle seq) =
    let map = obj()
    styles
    |> Seq.iter (fun (CssStyle(key, value)) ->
        map?(key) <- value)
    LitHtml.styleMap map