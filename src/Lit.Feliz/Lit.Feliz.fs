module Lit.Feliz

open System
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
    | Property of string * obj
    | Attribute of string * string
    | BooleanAttribute of string * bool
    | Event of string * obj
    | Fragment of Node list

let Html = HtmlEngine((fun t ns -> HtmlNode(t, List.ofSeq ns)), Text, fun () -> Fragment [])

let Svg = SvgEngine((fun t ns -> SvgNode(t, List.ofSeq ns)), Text, fun () -> Fragment [])

let Attr = AttrEngine((fun k v -> Attribute(k, v)), (fun k v -> BooleanAttribute(k, v)))

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
        LitBindings.styleMap map

    let cache = Constructors.WeakMap.Create<string[], string[]>()

    let strs (s: string[]) = s

    let buildTemplate (node: Node) =
        let rec addNode (parts, values) tag nodes =
            let styles', keyValuePairs =
                (([], []), nodes) ||> List.fold (fun (styles', kvs) node ->
                    match node with
                    | Style _ -> node::styles', kvs
                    | Property(key, value) -> styles', ("." + key, value)::kvs
                    | Attribute(key, value) -> styles', (key, box value)::kvs
                    | BooleanAttribute(key, value) -> styles', ("?" + key, box value)::kvs
                    | Event(key, value) -> styles', ("@" + key, box value)::kvs
                    | _ -> styles', kvs)

            let addKeyValuePairs (parts, values) keyValuePairs =
                let parts, values =
                    ((parts, values), keyValuePairs)
                    ||> List.fold (fun (parts, values) (key, value) ->
                        (" " + key + "=")::parts, value::values)
                ">"::parts, values

            let parts, values =
                match parts, styles', List.rev keyValuePairs with
                | [], _, _ -> failwith "unexpected empty parts"
                | head::parts, [], [] -> (head + "<" + tag + ">")::parts, values
                | head::parts, [], (fstKey, fstValue)::keyValuePairs ->
                    let parts = (head + "<" + tag + " " + fstKey + "=")::parts
                    let values = fstValue::values
                    addKeyValuePairs (parts, values) keyValuePairs
                | head::parts, styles', keyValuePairs ->
                    let parts = (head + "<" + tag + " style=")::parts
                    let values = (styles styles')::values
                    match keyValuePairs with
                    | [] -> ">"::parts, values
                    | keyValuePairs -> addKeyValuePairs (parts, values) keyValuePairs

            let parts, values = ((parts, values), nodes) ||> List.fold inner
            match parts with
            | [] -> failwith "unexpected empty parts"
            | head::parts -> (head + "</" + tag + ">")::parts, values

        and inner (parts, values) = function
            | Text v -> ""::parts, (box v::values)
            | Template v -> ""::parts, (box v::values)
            | Style _ | Property _ | Attribute _ | BooleanAttribute _ | Event _ -> parts, values
            | HtmlNode(tag, nodes)
            | SvgNode (tag, nodes) -> addNode (parts, values) tag nodes
            | Fragment nodes -> ((parts, values), nodes) ||> List.fold inner

        let parts, values = inner ([""], []) node
        let parts = List.rev parts |> List.toArray
        let values = List.rev values |> List.toArray

        parts, values

    let getValues (node: Node) =
        let rec addNode values nodes =
            let styles', newValues =
                (([], []), nodes) ||> List.fold (fun (styles', newValues) node ->
                    match node with
                    | Style _ -> node::styles', newValues
                    | Property(_key, value) -> styles', box value::newValues
                    | Attribute(_key, value) -> styles', box value::newValues
                    | BooleanAttribute(_key, value) -> styles', box value::newValues
                    | Event(_key, value) -> styles', box value::newValues
                    | _ -> styles', newValues)

            let values =
                match styles', newValues with
                | [], newValues -> newValues @ values
                | styles', newValues -> newValues @ [styles styles'] @ values

            (values, nodes) ||> List.fold inner

        and inner values = function
            | Text v -> box v::values
            | Template v -> box v::values
            | Style _ | Property _ | Attribute _ | BooleanAttribute _ | Event _ -> values
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
        | SvgNode _ -> LitBindings.svg.Invoke(strings, values)
        | _ -> LitBindings.html.Invoke(strings, values)

type Feliz =
    /// Unlike attributes, properties can accept non-string objects
    static member prop (key: string) (value: obj) =
        Property(key, value)

    /// Equivalent to lit-hmtl styleMap, accepting a list of Feliz styles
    static member styles (styles: Node seq) =
        Util.styles styles

    static member ofLit (template: TemplateResult) =
        Template template

    static member lit_html (s: FormattableString) =
        Feliz.ofLit(Lit.html s)

    static member lit_svg (s: FormattableString) =
        Feliz.ofLit(Lit.svg s)

    static member inline toLit (node: Node): TemplateResult =
        Util.getTemplate (emitJsExpr Util.strs "$0``") node
