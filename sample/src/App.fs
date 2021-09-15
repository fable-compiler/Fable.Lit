module App

open Browser.Types
open Fable.Core
open Fable.Core.JsInterop
open Elmish
open Lit
open Lit.Elmish
open Lit.Elmish.HMR

module Ionic =
    type Role =
        interface end

    type Popover =
        abstract present: unit -> JS.Promise<unit>
        abstract dismiss: unit -> JS.Promise<unit>
        abstract onDidDismiss: unit -> JS.Promise<Role>

    type PopoverOptions =
        abstract ``component``: string with get, set
        abstract componentProps: obj with get, set
        abstract event: Event with get, set
        abstract translucent: bool with get, set

    type PopoverController =
        abstract create: PopoverOptions -> JS.Promise<Popover>

    let popoverController: PopoverController = importMember "@ionic/core/dist/ionic/index.esm.js"

open Ionic

type Model = unit

type Msg =
    | OpenDocs
    | PopoverClosed of Role
    | OpenPopover of Event * componentName: string * componentProps: obj

let initialState() =
    (), Cmd.none

let update msg model =
    match msg with
    | OpenDocs ->
        JS.console.log("Open docs")
        model, Cmd.none
    | PopoverClosed role ->
        JS.console.log("Popover closed", role)
        model, Cmd.none
    | OpenPopover(ev, componentName, componentProps) ->
        model, promise {
            let! p = popoverController.create(jsOptions(fun o ->
                o.event <- ev
                o.translucent <- true
                o.``component`` <- componentName
                o.componentProps <- componentProps
            ))
            do! p.present()
            let! role = p.onDidDismiss()
            return PopoverClosed role
        } |> Cmd.OfPromise.result

[<LitElement("popover-content")>]
let PopoverContent() =
    let props = LitElement.init (fun cfg ->
        cfg.props <- {| dispatch = Prop.Of<Msg -> unit>()
                        // This property is assigned automatically by the popover
                        popover = Prop.Of<Popover>() |}
    )
    let dispatch = props.dispatch.Value
    let dismiss() = props.popover.Value.dismiss() |> Promise.start
    html $"""
      <ion-list>
        <ion-list-header>Ionic</ion-list-header>
        <ion-item button>Learn Ionic</ion-item>
        <ion-item button @click={fun _ -> dismiss(); dispatch OpenDocs}>
            Documentation
        </ion-item>
        <ion-item button>Showcase</ion-item>
        <ion-item button>GitHub Repo</ion-item>
        <ion-item lines="none" detail="false" button
                @click={fun _ -> dismiss()}>
            Close
        </ion-item>
      </ion-list>
    """

let view model dispatch =
    html $"""
  <ion-app>
    <ion-header translucent>
      <ion-toolbar>
        <ion-title>Popover</ion-title>

        <ion-buttons slot="end">
          <ion-button>
            <ion-icon slot="icon-only" ios="ellipsis-horizontal" md="ellipsis-vertical"></ion-icon>
          </ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>

    <ion-content fullscreen class="ion-padding">
      <ion-button expand="block" @click={fun (ev: Event) ->
        OpenPopover(ev, "popover-content", {| dispatch = dispatch |}) |> dispatch}>
        Show Popover
      </ion-button>
    </ion-content>
  </ion-app>
"""

let run() =
    Program.mkProgram initialState update view
    |> Program.withLit "app-container"
    |> Program.run