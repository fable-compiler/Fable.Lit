module Sample.App

open Browser
open Fable.Core
open Elmish
open Elmish.HMR
open Lit
open Lit.Elmish
open Components

let private hmr = HMR.createToken ()

Clock.register ()

let init () =
    { Value = "World"
      ShowClock = true
      ShowReact = true },
    Cmd.none

let update msg model =
    match msg with
    | ChangeValue v -> { model with Value = v }, Cmd.none
    | ToggleClock v -> { model with ShowClock = v }, Cmd.none
    | ToggleReact v -> { model with ShowReact = v }, Cmd.none

let lifecycle = html $"<life-cycle-controller></life-cycle-controller>"

let view model dispatch =
    html
        $"""
      <div class="vertical-container" style="margin-left: 2rem;">

        <button class="button"
            style="margin: 1rem 0"
            @click={Ev(fun _ -> not model.ShowReact |> ToggleReact |> dispatch)}>
            {if model.ShowReact then
                 "Hide"
             else
                 "Show"} React
        </button>

        {if not model.ShowReact then
             Lit.nothing
         else
             ReactLitComponent model.ShowClock}
        {if not model.ShowReact then
             Lit.nothing
         else
             lifecycle}

        <br />

        {elmishNameInput model.Value (ChangeValue >> dispatch)}
        {LocalNameInput()}
        <hello-there .name={model.Value}></hello-there>
        <hello-there-2></hello-there-2>

        {ClockDisplay model dispatch}

        <user-profile .name={model.Value} age="25"></user-profile>

        <element-with-controller><element-with-controller>
      </div>
    """

// Program.mkProgram init update view
// |> Program.withLit "app-container"
// |> Program.run

[<LitElement("sample-app")>]
let App () =
    Hook.useHmr (hmr)

    let _ =
        LitElement.init (fun config -> config.useShadowDom <- false
        // Technically it works
        // config.controllers <- {| elmish = Controller.Of(fun host -> ElmishController(host, init, update)) |}
        )

    let model, dispatch = Hook.useElmish (init, update)

    view model dispatch

open Experimental
open Fable.Core.JsInterop

[<LitElement("life-cycle-controller")>]
let LifeCycleController () =
    Hook.useHmr (hmr)
    let _ = LitElement.init ()
    let state, setState = Hook.useRefU false
    let txt, setTxt = Hook.useState ""

    Hook.GetController<EffectController>(
        (fun host ->
            JS.console.log (sprintf "Connected!")
            Callback),
        (fun host ->
            JS.console.log (sprintf "Update! %A" state.Value)
            Callback),
        (fun host ->
            JS.console.log (sprintf "Updated! %A" state.Value)
            Callback),
        (fun host ->
            JS.console.log (sprintf "Disconnected!")
            Callback)
    )
    |> ignore

    html
        $"""
        <label for="">
            Checked:
            <input type="checkbox" @change={fun _ -> setState (state.Value |> not)} >
        </label>
        <label for="">
            Text: {txt}
            <input type="text" @input={EvVal(setTxt)} >
        </label>
    """

registerElement<ClassComponents.UserProfile> "user-profile" {
    property "name" {
        use_attribute
        use_type PropType.String
    }

    property "age" {
        use_attribute
        use_type PropType.Number
    }

    css $"p {{ color: red; }}"
}

registerElement<ClassComponents.ElementWithController> "element-with-controller" {
    css $"p {{ color: red; }}"
    css $"li {{ color: rebeccapurple; }}"
}
