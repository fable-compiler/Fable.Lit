module ImportTemplate

open Lit

ImportSideEffect.setValue(3)

let render (author: string) =
    html $"""<p>I was loaded by {author} just in time!</p>"""
