module LitElement

open Fable.Core.JsInterop
open Lit
open Expect
open Expect.Dom
open WebTestRunner
open Lit.Test

let private hmr = HMR.createToken()

[<LitElement("fable-element")>]
let MyEl () =
    Hook.useHmr(hmr)
    let _ = LitElement.init ()

    html
        $"""
        <p>Element</p>
    """

[<LitElement("fel-attribute-changes")>]
let AttributeChanges () =
    let host =
        LitElement.init (fun config -> config.props <- {| fName = Prop.Of("default", attribute = "f-name") |})

    html
        $"""
        <p id="value">{host.props.fName.Value}</p>
    """

[<LitElement("fel-attribute-doesnt-change")>]
let AttributeDoesntChange () =
    let host =
        LitElement.init (fun config -> config.props <- {| fName = Prop.Of("default", attribute = "") |})

    html
        $"""
        <p id="value">{host.props.fName.Value}</p>
    """

let reverse (str: string) =
    str.ToCharArray() |> Array.rev

[<LitElement("fel-attribute-reflects")>]
let AttributeReflects () =
    let host =
        LitElement.init (fun config ->
            config.props <-
                {|
                    fName = Prop.Of("default", attribute = "f-name", reflect = true)
                    revName = Prop.Of([||], attribute = "rev-name", fromAttribute=reverse)
                |})

    html
        $"""
        <p id="f-value">{host.props.fName.Value}</p>
        <p id="rev-value">{host.props.revName.Value |> Array.map string |> String.concat "-"}</p>
    """

[<LitElement("fel-dispatch-events")>]
let DispatchEvents () =
    let el = LitElement.init ()

    let onClick _ =
        el.dispatchEvent("fires-events")

    html
        $"""
        <button @click={onClick}>Click me!</button>
    """

[<LitElement("fel-dispatch-custom-events")>]
let DispatchCustomEvents () =
    let el = LitElement.init()

    let onClick _ =
        el.dispatchCustomEvent("fires-custom-events", 10)

    html
        $"""
        <button @click={onClick}>Click me!</button>
    """

describe "LitElement" <| fun () ->
    it "fable-element renders" <| fun () -> promise {
        use! el = render_html $"<fable-element></fable-element>"
        return! el.El |> Expect.matchHtmlSnapshot "fable-element"
    }

    it "Reacts to attribute/property changes" <| fun () -> promise {
        use! el = render_html $"<fel-attribute-changes></fel-attribute-changes>"
        let el = el.El
        // check the default value
        el.getSelector("#value") |> Expect.innerText "default"
        el.setAttribute("f-name", "fable")
        // wait for lit's render updates
        do! elementUpdated el
        el.getSelector("#value") |> Expect.innerText "fable"
        // update property manually
        el?fName <- "fable-2"
        do! elementUpdated el
        el.getSelector("#value") |> Expect.innerText "fable-2"
    }

    it "Doesn't react to attribute changes" <| fun () -> promise {
        use! el = render_html $"<fel-attribute-doesnt-change></fel-attribute-doesnt-change>"
        let el = el.El
        // check the default value
        el.getSelector("#value") |> Expect.innerText "default"
        el.setAttribute("f-name", "fable")
        // wait for lit's render updates
        do! elementUpdated el
        el.getSelector("#value") |> Expect.innerText "default"
        // update property manually
        el?fName <- "fable"
        do! elementUpdated el
        el.getSelector("#value") |> Expect.innerText "fable"
    }

    it "Reflect Attribute changes" <| fun () -> promise {
        use! el = render_html $"<fel-attribute-reflects></fel-attribute-reflects>"
        let el = el.El
        // check the default value
        el.getSelector("#f-value") |> Expect.innerText "default"
        el.getAttribute("f-name") |> Expect.equal "default"
        el?fName <- "fable"
        // wait for lit's render updates
        do! elementUpdated el
        // setting the property should have updated the value and the attribute
        el.getSelector("#f-value") |> Expect.innerText "fable"
        el.getAttribute("f-name") |> Expect.equal "fable"
    }

    it "From Attribute works" <| fun () -> promise {
        use! el = render_html $"""<fel-attribute-reflects rev-name="aloh"></fel-attribute-reflects>"""
        let el = el.El
        el.getSelector("#rev-value") |> Expect.innerText "h-o-l-a"
    }

    it "Fires Events" <| fun () -> promise {
        use! el = render_html $"""<fel-dispatch-events></fel-dispatch-events>"""
        let el = el.El
        do!
            Expect.dispatch
                "fires-events"
                (fun _ -> el.shadowRoot.getButton("click").click())
                el
    }

    it "Fires Custom Events" <| fun () -> promise {
        use! el = render_html $"""<fel-dispatch-custom-events></fel-dispatch-custom-events>"""
        let el = el.El
        let! result =
            Expect.dispatchCustom<int>
                "fires-custom-events"
                (fun _ -> el.shadowRoot.getButton("click").click())
                el

        Expect.equal (Some 10) result
    }
