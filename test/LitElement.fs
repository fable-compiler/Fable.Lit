module LitElement

open Fable.Core
open Browser.Types

open Lit



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

[<LitElement("fel-attribute-reflects")>]
let AttributeReflects () =
    let props =
        LitElement.init
            (fun config -> config.props <- {| fName = Prop.Of("default", attribute = "f-name", reflect = true) |})

    html
        $"""
        <p id="value">{props.fName.Value}</p>
    """
