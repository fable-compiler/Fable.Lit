module Lit.Parser

open System.Collections.Generic
open System.Text.RegularExpressions
open Fable.Core

type ParseResult =
    | ParseSuccess of groups: string list * nextIndex: int
    | ParseFail

type Matcher = string -> int -> ParseResult

/// ATTENTION: Forward slashes / must be escaped
[<Emit("/$0!/y")>]
let stickyRegex(pattern: string): Regex = jsNative

let stickyRegexToMatcher (r: Regex) =
    fun str index ->
        let m = r.Match(str, index)
        if not m.Success then
            ParseFail
        else
            let index = index + m.Length
            let groupsLen = m.Groups.Count
            let groups =
                if groupsLen > 1 then
                    let mutable groups = []
                    for i = groupsLen - 1 to 0 do
                        groups <- m.Groups.[i].Value::groups
                    groups
                else
                    [m.Groups.[0].Value]
            ParseSuccess(groups, index)

let WHITESPACE =
    stickyRegex @"\s+"
    |> stickyRegexToMatcher

let consumeWhitespace (str: string) (index: int) =
    match WHITESPACE str index with
    | ParseFail -> index
    | ParseSuccess(_, index2) ->
        // printfn "Consumed whitespace from %i to %i: <<%s>>" index index2 str.[index..index2]
        index2

let BETWEEN (quotes: ISet<char>) (openChar: char) (closeChar: char) (str: string) (index: int) =
    let rec tryFindClosing openQuote depth index =
        match openQuote with
        | _ when index >= str.Length -> None

        | Some openQuote ->
            if str.[index] = openQuote && str.[index - 1] <> '\\' then
                tryFindClosing None depth (index + 1)
            else
                tryFindClosing (Some openQuote) depth (index + 1)

        | None ->
            let cur = str.[index]
            if cur = closeChar then
                if depth = 0 then Some index
                else tryFindClosing None (depth - 1) (index + 1)

            elif cur = openChar then
                tryFindClosing None (depth + 1) (index + 1)

            elif quotes.Contains(cur) && str.[index - 1] <> '\\' then
                tryFindClosing (Some cur) depth (index + 1)

            else tryFindClosing None depth (index + 1)

    if str.[index] = openChar then
        match tryFindClosing None 0 (index + 1) with
        // Let's consider open groups a parsing success
        | None -> ParseSuccess([str.[index..] ], str.Length)
        | Some index2 -> ParseSuccess([str.[index .. index2 - 1] ], index2 + 1)
    else
        ParseFail

let NEXT_CHAR (quotes: ISet<char>) (char: char) (str: string) (index: int) =
    let rec tryFindChar openQuote index =
        match openQuote with
        | _ when index = str.Length -> None

        | Some openQuote ->
            if str.[index] = openQuote && str.[index - 1] <> '\\' then
                tryFindChar None (index + 1)
            else
                tryFindChar (Some openQuote) (index + 1)

        | None ->
            let cur = str.[index]
            if cur = char then
                Some index

            elif quotes.Contains(cur) && str.[index - 1] <> '\\' then
                tryFindChar (Some cur) (index + 1)

            else tryFindChar None (index + 1)

    match tryFindChar None index with
    | None -> ParseFail
    | Some index2 -> ParseSuccess([], index2 + 1)

module Css =
    let IDENT =
        stickyRegex "[\w\-]+"
        |> stickyRegexToMatcher

    let BETWEEN_CURLY_BRACES =
        BETWEEN (HashSet ['"'; '\'']) '{' '}'

    let NEXT_SEMICOLON =
        NEXT_CHAR (HashSet ['"'; '\'']) ';'

    let NEXT_OPEN_CURLY_BRACE =
        NEXT_CHAR (HashSet []) '{'

    let scope (uniqueIdent: string) (rules: string) =
        let keyframes = ResizeArray()

        let fail (rules: string) index =
            failwithf $"Cannot parse CSS at {index}: ..{rules.[index..index+8]}.."

        let scopeSelector (selector: string) =
            let index = consumeWhitespace selector 0
            if selector.[index..index + 4] = ":host" then
                selector.[0..index - 1] + "." + uniqueIdent + selector.[index + 5..]
            else
                selector.[0..index - 1] + "." + uniqueIdent + " " + selector.[index..]

        let rec skipCurlyBracesFromOpenBrace (scoped: string) (rules: string) (index: int) =
            match BETWEEN_CURLY_BRACES rules index with
            | ParseFail -> fail rules index
            | ParseSuccess(_, index2) ->
                let scoped = scoped + rules.[index..index2 - 1]
                // printfn "Skipped curly braces: <<%s>>" rules.[index..index2 - 1]
                scope scoped rules index2

        and skipCurlyBraces (scoped: string) (rules: string) (index: int) =
            match NEXT_OPEN_CURLY_BRACE rules index with
            | ParseFail -> fail rules index
            | ParseSuccess(_, index2) ->
                let scoped = scoped + rules.[index..index2 - 2]
                skipCurlyBracesFromOpenBrace scoped rules (index2 - 1)

        and scope (scoped: string) (rules: string) (index: int) =
            let index2 = consumeWhitespace rules index
            if index2 >= rules.Length then
                scoped + rules.[index..]
            else
                if rules.[index2] = '@' then
                    match IDENT rules (index2 + 1) with
                    | ParseSuccess(statement, index2) ->
                        match statement with
                        // Conditional: @media, @supports
                        | ["media"|"supports"] ->
                            match NEXT_OPEN_CURLY_BRACE rules index2 with
                            | ParseFail -> fail rules index2
                            | ParseSuccess(_, index2) ->
                                let scoped = scoped + rules.[index..index2 - 2]
                                match BETWEEN_CURLY_BRACES rules (index2 - 1) with
                                | ParseFail -> fail rules index2
                                | ParseSuccess(_, index3) ->
                                    let scoped2 = scope "" rules.[index2..index3-2] 0
                                    let scoped = scoped + "{" + scoped2 + "}"
                                    scope scoped rules index3

                        // Nested: @keyframes, @font-face, @page,
                        | ["keyframes"] ->
                            let index2 = consumeWhitespace rules index2
                            match IDENT rules index2 with
                            | ParseSuccess([ident], index3) ->
                                keyframes.Add(ident)
                                let scoped = scoped + rules.[index..index2 - 1] + uniqueIdent + "-" + ident
                                skipCurlyBraces scoped rules index3
                            | _ -> fail rules index2

                        | ["font-face"|"page"] ->
                            let scoped = scoped + rules.[index..index2 - 1]
                            skipCurlyBraces scoped rules index2

                        // Regular @charset, @import, @namespace
                        | _ ->
                            match NEXT_SEMICOLON rules index2 with
                            | ParseFail -> fail rules index2
                            | ParseSuccess(_, index2) ->
                                let scoped = scoped + rules.[index..index2 - 1]
                                scope scoped rules index2

                    | ParseFail -> fail rules (index2 + 1)

                // Selectors
                else
                    match NEXT_OPEN_CURLY_BRACE rules index2 with
                    | ParseFail -> fail rules index2
                    | ParseSuccess(_, index3) ->
                        let selectors =
                            rules.[index2..index3 - 2].Split(',')
                            |> Array.map scopeSelector
                            |> String.concat ","

                        let scoped = scoped + rules.[index..index2 - 1] + selectors
                        skipCurlyBracesFromOpenBrace scoped rules (index3 - 1)

        // Remove comments
        let rules = Regex(@"\/\*(?:(?!\*\/)[\s\S])*\*\/").Replace(rules, "")
        let rules = scope "" rules 0

        if keyframes.Count = 0 then rules
        else
            Regex("(animation(?:-name)?:)([^};]+)").Replace(rules, fun m ->
                m.Groups.[1].Value + Regex(@"[\w\-]+").Replace(m.Groups.[2].Value, fun m ->
                    let ident = m.Value
                    if keyframes.Contains(ident) then uniqueIdent + "-" + ident else ident))
