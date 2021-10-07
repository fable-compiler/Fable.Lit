module DirectiveTest

open Lit
open Expect
open Expect.Dom
open WebTestRunner
open Lit.Test

let mutable private onChangeSideEffect = 0

let onChangeTemplate (name: string) (age: int) =
    onChangeSideEffect <- onChangeSideEffect + 1
    html $"<p>Hey {name}! You are {age} years old!</p>"

describe "Directive" <| fun () ->
    it "works with onChange" <| fun () -> promise {
        use! container = render_html $"<div></div>"
        let el = container.El

        onChangeSideEffect |> Expect.equal 0

        Lit.onChange("Angel", 10, onChangeTemplate) |> Lit.render el

        onChangeSideEffect |> Expect.equal 1
        el.innerText |> Expect.equal "Hey Angel! You are 10 years old!"

        Lit.onChange("Angel", 10, onChangeTemplate) |> Lit.render el
        onChangeSideEffect |> Expect.equal 1

        Lit.onChange("Angel", 20, onChangeTemplate) |> Lit.render el
        onChangeSideEffect |> Expect.equal 2
        el.innerText |> Expect.equal "Hey Angel! You are 20 years old!"
    }

    it "works with ofPromise" <| fun () -> promise {
        use! container = render_html $"<div></div>"
        let el = container.El

        let deferredTemplate = promise {
            do! Promise.sleep 80
            return html $"<p>Sorry for being late!</p>"
        }

        Lit.ofPromise(deferredTemplate, placeholder=html $"<p>I'm already here!</p>")
        |> Lit.render el

        el.innerText |> Expect.equal "I'm already here!"
        do! Promise.sleep 100
        el.innerText |> Expect.equal "Sorry for being late!"
    }

    it "works with ofImport" <| fun () -> promise {
        use! container = render_html $"<div></div>"
        let el = container.El

        ImportSideEffect.getValue() |> Expect.equal 0
        el.innerText |> Expect.equal ""

        Lit.ofImport(ImportTemplate.render, fun render -> render "a test")
        |> Lit.render el
        do! Promise.sleep 50

        el.innerText |> Expect.equal "I was loaded by a test just in time!"
        ImportSideEffect.getValue() |> Expect.equal 3
    }
