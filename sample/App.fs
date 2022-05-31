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
    let _ = LitElement.init (fun config -> config.useShadowDom <- false)
    let model, dispatch = Hook.useElmish (init, update)
    view model dispatch

open Experimental

[<LitElementExperimental(true)>]
let Sample () =
    let name = Controllers.GetProperty("name", "Peter")

    let state: StateController<int> =
        Controllers.GetController<StateController<int>>(10)

    let twoArgs = Controllers.GetController<TwoArgsCtrl>(10, 20)


    let inline updateValues _ =
        state.SetState(state.Value + 1)
        let (initial, secondial) = twoArgs.Values
        twoArgs.updateValues ((initial + 1, secondial + 2))


    html $"<p>{state.Value}, {twoArgs.Values}, {name}</p> <button @click={updateValues}>Increment</button>"


registerFuncElement ("hello-there", Sample) {
    css $"p {{ color: blue; }}"

    property "name" {
        use_attribute
        use_type PropType.String
    }
}
// registerFuncElement ("hello-there-2", Sample2) { css $"p {{ color: blue; }}" }

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
