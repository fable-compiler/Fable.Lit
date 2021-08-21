namespace Feliz

open Fable.Core

[<Erase>]
type CssStyle = CssStyle of key: string * value: string

[<AutoOpen>]
module Css =
    let Css = CssEngine(fun k v -> CssStyle(k, v))