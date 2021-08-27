module Lit.Feliz

open Fable.Core
open Fable.Core.JsInterop
open Feliz
open Lit

type Node =
    | Text of string
    | Template of TemplateResult
    | Style of string * string
    | HtmlNode of string * Node list
    | SvgNode of string * Node list
    | Attr of string * string
    | BoolAttr of string * bool
    | Event of string * obj
    | Fragment of Node list

let Html = HtmlEngine((fun t ns -> HtmlNode(t, List.ofSeq ns)), Text, fun () -> Fragment [])

let Svg = SvgEngine((fun t ns -> SvgNode(t, List.ofSeq ns)), Text, fun () -> Fragment [])

let Attr = AttrEngine((fun k v -> Attr(k, v)), (fun k v -> BoolAttr(k, v)))

let Css = CssEngine(fun k v -> Style(k, v))

let Ev = EventEngine(fun k f -> Event(k.ToLowerInvariant(), f))

module Util =
    open Fable.Core.JS

    let styles (styles: Node seq) =
        let map = obj()
        styles
        |> Seq.iter (function
            | Style(key, value) -> map?(key) <- value
            | _ -> ())
        LitHtml.styleMap map

    let cache = Constructors.WeakMap.Create<string[], string[]>()

    let strs (s: string[]) = s

    let buildTemplate (node: Node) =
        let rec addNode (parts, values) tag nodes =
            let styles', attrs =
                (([], []), nodes) ||> List.fold (fun (styles', attrs) node ->
                    match node with
                    | Style _ -> node::styles', attrs
                    | Attr(key, value) -> styles', (key, box value)::attrs
                    | BoolAttr(key, value) -> styles', ("?" + key, box value)::attrs
                    | Event(key, value) -> styles', ("@" + key, box value)::attrs
                    | _ -> styles', attrs)

            let addAttrs (parts, values) attrs =
                let parts, values =
                    ((parts, values), attrs) ||> List.fold (fun (parts, values) (key, value) ->
                        (" " + key + "=")::parts, value::values)
                ">"::parts, values

            let parts, values =
                match parts, styles', List.rev attrs with
                | [], _, _ -> failwith "unexpected empty parts"
                | head::parts, [], [] -> (head + "<" + tag + ">")::parts, values
                | head::parts, [], (fstKey, fstValue)::attrs ->
                    let parts = (head + "<" + tag + " " + fstKey + "=")::parts
                    let values = fstValue::values
                    addAttrs (parts, values) attrs
                | head::parts, styles', attrs ->
                    let parts = (head + "<" + tag + " style=")::parts
                    let values = (styles styles')::values
                    match attrs with
                    | [] -> ">"::parts, values
                    | attrs -> addAttrs (parts, values) attrs

            let parts, values = ((parts, values), nodes) ||> List.fold inner
            match parts with
            | [] -> failwith "unexpected empty parts"
            | head::parts -> (head + "</" + tag + ">")::parts, values

        and inner (parts, values) = function
            | Text v -> ""::parts, (box v::values)
            | Template v -> ""::parts, (box v::values)
            | Style _ | Attr _ | BoolAttr _ | Event _ -> parts, values
            | HtmlNode(tag, nodes)
            | SvgNode (tag, nodes) -> addNode (parts, values) tag nodes
            | Fragment nodes -> ((parts, values), nodes) ||> List.fold inner

        let parts, values = inner ([""], []) node
        let parts = List.rev parts |> List.toArray
        let values = List.rev values |> List.toArray

        parts, values

    let getValues (node: Node) =
        let rec addNode values nodes =
            let styles', attrs =
                (([], []), nodes) ||> List.fold (fun (styles', attrs) node ->
                    match node with
                    | Style _ -> node::styles', attrs
                    | Attr(_key, value) -> styles', box value::attrs
                    | BoolAttr(_key, value) -> styles', box value::attrs
                    | Event(_key, value) -> styles', box value::attrs
                    | _ -> styles', attrs)

            let values =
                match styles', attrs with
                | [], attrs -> attrs @ values
                | styles', attrs -> attrs @ [styles styles'] @ values

            (values, nodes) ||> List.fold inner

        and inner values = function
            | Text v -> box v::values
            | Template v -> box v::values
            | Style _ | Attr _ | BoolAttr _ | Event _ -> values
            | HtmlNode(_tag, nodes)
            | SvgNode (_tag, nodes) -> addNode values nodes
            | Fragment nodes -> (values, nodes) ||> List.fold inner

        inner [] node |> List.rev |> List.toArray

    let getTemplate (ref: string[]) (node: Node) =
        let strings = cache.get(ref)
        let strings, values =
            if isNull strings then
                let strings, values = buildTemplate node
                cache.set(ref, strings) |> ignore
                strings, values
            else
                let values = getValues node
                strings, values
        match node with
        | SvgNode _ -> LitHtml.svg.Invoke(strings, values)
        | _ -> LitHtml.html.Invoke(strings, values)

[<RequireQualifiedAccess>]
module Feliz =
    open System

    /// Equivalent to lit-hmtl styleMap, accepting a list of Feliz styles
    let styles (styles: Node seq) =
        Util.styles styles

    let ofLit (template: TemplateResult) =
        Template template

    let lit_html (s: FormattableString) =
        ofLit (html s)

    let lit_svg (s: FormattableString) =
        ofLit (svg s)

    let inline toLit (node: Node): TemplateResult =
        Util.getTemplate (emitJsExpr Util.strs "$0``") node
