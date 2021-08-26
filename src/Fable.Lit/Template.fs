module Template

open System
open Fable.Core

type JsTag<'T> = delegate of strs: string[] * [<ParamArray>] args: obj[] -> 'T
type Tag<'T> = FormattableString -> 'T

let transform (tag: JsTag<'T>): Tag<'T> =
    fun fmt -> tag.Invoke(fmt.GetStrings(), fmt.GetArguments())
