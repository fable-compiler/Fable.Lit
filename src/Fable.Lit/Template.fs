module Template

open System
open System.Text.RegularExpressions

type JsTag<'T> = delegate of strs: string[] * [<ParamArray>] args: obj[] -> 'T
type Tag<'T> = FormattableString -> 'T

let transform (tag: JsTag<'T>): Tag<'T> =
    fun fmt ->
        let strs = Regex(@"\{\d+\}").Split(fmt.Format)
        let args = fmt.GetArguments()
        tag.Invoke(strs, args)
