module App

open Browser
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
        abstract ``component``: obj with get, set
        abstract componentProps: obj with get, set
        abstract event: Event with get, set
        abstract translucent: bool with get, set

    type PopoverController =
        abstract create: PopoverOptions -> JS.Promise<Popover>

    let popoverController: PopoverController = importMember "@ionic/core/dist/ionic/index.esm.js"

    let showPopover (render: Popover -> Lit.TemplateResult) (ev: Event) =
        promise {
            let contentEl = document.createElement("div")
            let! p = popoverController.create(jsOptions(fun o ->
                o.event <- ev
                o.translucent <- true
                o.``component`` <- contentEl
            ))
            render p |> Lit.render contentEl
            do! p.present()
            return p
        }

open Ionic

type Model = unit

type Msg =
    | OpenDocs

let initialState() =
    (), Cmd.none

let update msg model =
    match msg with
    | OpenDocs ->
        console.log("Open docs")
        model, Cmd.none

let popoverContent dispatch (p: Popover)  =
    let dismiss() = p.dismiss() |> Promise.start
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
      <ion-button expand="block" @click={showPopover (popoverContent dispatch)}>
        Show Popover
      </ion-button>
    </ion-content>
  </ion-app>
"""

let run() =
    Program.mkProgram initialState update view
    |> Program.withLit "app-container"
    |> Program.run