module Sample.Components

open Browser.Types
open Lit

let private hmr = HMR.createToken()

module ReactLib =
    open Feliz

    [<ReactComponent>]
    let MyComponent showClock =
        let state, setState = React.useState 0
        React.useEffectOnce((fun () ->
            printfn "Initializing React component..."
            React.createDisposable (fun () ->
                printfn "Disposing React component..."
            )
        ))

        Html.div [
            prop.className "card"
            prop.children [
                Html.div [
                    prop.className "card-content"
                    prop.children [
                        Html.div [
                            prop.className "content"
                            prop.children [
                                Html.p $"""I'm a React component. Clock is {if showClock then "visible" else "hidden"}"""
                                Html.button [
                                    prop.className "button"
                                    prop.onClick(fun _ -> setState(state + 1))
                                    prop.text $"""Clicked {state} time{if state = 1 then "" else "s"}!"""
                                ]
                            ]
                        ]
                    ]
                ]
            ]
        ]

let ReactLitComponent =
    React.toLit ReactLib.MyComponent

module Styles =
    let verticalContainer =
        inline_css """.{
            margin-left: 2rem;
            display: flex;
            justify-content: center;
            align-items: center;
            flex-direction: column;
        }"""

    let nameInput (color: string) =
        inline_css $""".{{
            color: {color};
            background-color: lavender;
            padding: 0.25rem;
            font-size: 16px;
            width: 250px;
            margin-bottom: 0.5rem;
        }}"""

let toggleVisible (txt: string) (isVisible: bool) (isEnabled: bool) (onClick: Event -> unit) =
    html $"""
        <button class="button"
                style="margin: 1rem 0"
                @click={Ev onClick}
                ?disabled={not isEnabled}>
          {if isVisible then Lit.ofText $"Hide {txt}"
           else html $"<strong>Show {txt}</strong>"}
        </button>
    """

// This render function integrates with Elmish and doesn't keep local state
let elmishNameInput value dispatch =
    html $"""
      <div class="content">
        <p>Elmish state: <i>Hello {value}!</i></p>
        <input
          value={value}
          style={Styles.nameInput value}
          @keyup={EvVal dispatch}>
      </div>
    """

// This function keeps local state and can use hooks
[<HookComponent>]
let LocalNameInput() =
    Hook.useHmr(hmr)
    let value, setValue = Hook.useState "Local"
    let inputRef = Hook.useRef<HTMLInputElement>()

    html $"""
      <div class="content">
        <p>Local state: <i>Hello {value}!</i></p>
        <input
          {Lit.refValue inputRef}
          value={value}
          style={Styles.nameInput value}
          @keyup={EvVal setValue}
          @focus={Ev(fun _ ->
            inputRef.Value
            |> Option.iter (fun el -> el.select()))}>
      </div>
    """

[<HookComponent>]
let ClockDisplay model dispatch =
    let transition =
        Hook.useTransition(
            ms = 800,
            cssBefore = inline_css $""".{{
                opacity: 0;
                transform: scale(2) rotate(1turn)
            }}""",
            cssAfter = inline_css $""".{{
                opacity: 0;
                transform: scale(0.1) rotate(-1.5turn)
            }}""",
            onComplete = fun isIn ->
                if model.ShowClock <> isIn then dispatch ToggleClock
        )

    let clockContainer() =
        html $"""
            <div style={transition.css}>
                <my-clock
                    minute-colors="white, red, yellow, purple"
                    hour-color="yellow"></my-clock>
            </div>
        """

    let isButtonEnabled = not transition.isRunning
    html $"""
        <div style="{Styles.verticalContainer}">
            {toggleVisible "Clock" model.ShowClock isButtonEnabled (fun _ ->
                transition.trigger(not model.ShowClock))}

            {if transition.hasLeft then Lit.nothing else clockContainer()}
        </div>
    """
