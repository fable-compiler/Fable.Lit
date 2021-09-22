module LitElement

open Fable.Core.JsInterop
open Lit
open Expect
open Expect.Dom
open WebTestRunner

[<LitElement("fable-element")>]
let MyEl () =
    LitElement.init ()

    html
        $"""
        <p>Element</p>
    """

[<LitElement("fel-attribute-changes")>]
let AttributeChanges () =
    let props =
        LitElement.init (fun config -> config.props <- {| fName = Prop.Of("default", attribute = "f-name") |})

    html
        $"""
        <p id="value">{props.fName.Value}</p>
    """

[<LitElement("fel-attribute-doesnt-change")>]
let AttributeDoesntChange () =
    let props =
        LitElement.init (fun config -> config.props <- {| fName = Prop.Of("default", attribute = "") |})

    html
        $"""
        <p id="value">{props.fName.Value}</p>
    """

let reverse (str: string) =
    str.ToCharArray() |> Array.rev

[<LitElement("fel-attribute-reflects")>]
let AttributeReflects () =
    let props =
        LitElement.init (fun config ->
            config.props <-
                {|
                    fName = Prop.Of("default", attribute = "f-name", reflect = true)
                    revName = Prop.Of([||], attribute = "rev-name", fromAttribute=reverse)
                |})

    html
        $"""
        <p id="f-value">{props.fName.Value}</p>
        <p id="rev-value">{props.revName.Value |> Array.map string |> String.concat "-"}</p>
    """

describe "LitElement" <| fun () ->
    it "fable-element renders" <| fun () -> promise {
        use! el = render_html $"<fable-element></fable-element>"
        return! el.El |> Expect.matchShadowRootSnapshot "fable-element"
    }

    // it "Can render LitElement as function" <| fun () -> promise {
    //     let! el = fixture_html $"{AttributeChanges()}"
    //     return! el |> Expect.matchShadowRootSnapshot "fel-attribute-changes"
    // }

    it "Reacts to attribute/property changes" <| fun () -> promise {
        use! el = render_html $"<fel-attribute-changes></fel-attribute-changes>"
        let el = el.El
        // check the default value
        el.shadowRoot.querySelector("#value") |> Expect.innerText "default"
        el.setAttribute("f-name", "fable")
        // wait for lit's render updates
        do! el.updateComplete
        el.shadowRoot.querySelector("#value") |> Expect.innerText "fable"
        // update property manually
        el?fName <- "fable-2"
        do! el.updateComplete
        el.shadowRoot.querySelector("#value") |> Expect.innerText "fable-2"
    }

    it "Doesn't react to attribute changes" <| fun () -> promise {
        use! el = render_html $"<fel-attribute-doesnt-change></fel-attribute-doesnt-change>"
        let el = el.El
        // check the default value
        el.shadowRoot.querySelector("#value") |> Expect.innerText "default"
        el.setAttribute("f-name", "fable")
        // wait for lit's render updates
        do! el.updateComplete
        el.shadowRoot.querySelector("#value") |> Expect.innerText "default"
        // update property manually
        el?fName <- "fable"
        do! el.updateComplete
        el.shadowRoot.querySelector("#value") |> Expect.innerText "fable"
    }

    it "Reflect Attribute changes" <| fun () -> promise {
        use! el = render_html $"<fel-attribute-reflects></fel-attribute-reflects>"
        let el = el.El
        // check the default value
        el.shadowRoot.querySelector("#f-value") |> Expect.innerText "default"
        el.getAttribute("f-name") |> Expect.equal "default"
        el?fName <- "fable"
        // wait for lit's render updates
        do! el.updateComplete
        // setting the property should have updated the value and the attribute
        el.shadowRoot.querySelector("#f-value") |> Expect.innerText "fable"
        el.getAttribute("f-name") |> Expect.equal "fable"
    }

    it "From Attribute works" <| fun () -> promise {
        use! el = render_html $"""<fel-attribute-reflects rev-name="aloh"></fel-attribute-reflects>"""
        let el = el.El
        el.shadowRoot.querySelector("#rev-value") |> Expect.innerText "h-o-l-a"
    }
