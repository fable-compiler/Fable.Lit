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

// This render function integrates with Elmish and doesn't keep local state
let elmishNameInput value dispatch =
    html $"""
      <div class="content">
        <p>Elmish state: <i>Hello {value}!</i></p>
        <input class="name-input"
          value={value}
          @keyup={EvVal dispatch}>
      </div>
    """

// This function keeps local state and can use hooks
[<HookComponent>]
let LocalNameInput() =
    Hook.useHmr(hmr)
    let value, setValue = Hook.useState "Local"
    let inputRef = Hook.useRef<HTMLInputElement>()
    let className = Hook.use_scoped_css """
        input {
            border-radius: 7.5px;
        }
        """

    html $"""
      <div class="{className} content">
        <p>Local state: <i>Hello {value}!</i></p>
        <input class="name-input"
          {Lit.refValue inputRef}
          value={value}
          @keyup={EvVal setValue}
          @focus={Ev(fun _ ->
            inputRef.Value
            |> Option.iter (fun el -> el.select()))}>
      </div>
    """

[<HookComponent>]
let ClockDisplay model dispatch =
    Hook.useHmr (hmr)
    let transitionMs = 800
    let clasName = Hook.use_scoped_css $"""
        .clock-container {{
            transition-duration: {transitionMs}ms;
        }}
        .clock-container.transition-enter {{
            opacity: 0;
            transform: scale(2) rotate(1turn);
        }}
        .clock-container.transition-leave {{
            opacity: 0;
            transform: scale(0.1) rotate(-1.5turn);
        }}

        @keyframes move-side-by-side {{
            from {{ margin-left: -50%%; }}
            to {{ margin-left: 50%%; }}
        }}
        button {{
            animation: 1.5s linear 1s infinite alternate move-side-by-side;
        }}
        """

    let transition =
        Hook.useTransition(
            transitionMs,
            onEntered = (fun () -> ToggleClock true |> dispatch),
            onLeft = (fun () -> ToggleClock false |> dispatch))

    let clockContainer() =
        html $"""
            <div class="clock-container {transition.className}">
                <my-clock
                    minute-colors="white, red, yellow, purple"
                    hour-color="yellow"></my-clock>
            </div>
        """

    html $"""
        <div class="{clasName} vertical-container">

            <button class="button"
                style="margin: 1rem 0"
                ?disabled={transition.isRunning}
                @click={Ev(fun _ ->
                    if model.ShowClock then transition.triggerLeave()
                    else transition.triggerEnter())}>
                {if model.ShowClock then "Hide" else "Show"} clock
            </button>

            {if transition.hasLeft then Lit.nothing else clockContainer()}
        </div>
    """

type Components =
    static member Button(text: string, onClick: unit->unit, ?enabled: bool) =
        let enabled = defaultArg enabled true
        html $"""
        <button class="button"
            @click={Ev(fun _ -> onClick())}
            ?disabled={not enabled}>
            {text}
        </button>
        """

    [<HookComponent>]
    static member ModalButton(text: string) =
        let active, setActive = Hook.useState(false)
        html $"""
        <button class="button"
            @click={Ev(fun _ -> setActive true)}
            >
            Open modal
        </button>
        <div class={Lit.classes ["modal", true;  "is-active", active]}>
            <div class="modal-background"></div>
            <div class="modal-content">
                <div class="notification">
                    {text}
                </div>
            </div>
            <button class="modal-close is-large"
                @click={Ev(fun _ -> setActive(false))}
                ></button>
        </div>
        """

    [<HookComponent>]
    static member ModalButtonStyled(text: string) =
        Hook.useHmr(hmr)
        let active, setActive = Hook.useState(false)
        let className = Hook.use_scoped_css """
            .modal-content > .notification {
                margin: 0 auto;
                max-width: 500px;
            }
        """

        html $"""
        <div class={className}>
            <!-- button and modal -->
            <button class="button"
                @click={Ev(fun _ -> setActive true)}
                >
                Open modal
            </button>
            <div class={Lit.classes ["modal", true;  "is-active", active]}>
                <div class="modal-background"></div>
                <div class="modal-content">
                    <div class="notification">
                        {text}
                    </div>
                </div>
                <button class="modal-close is-large"
                    @click={Ev(fun _ -> setActive(false))}
                    ></button>
            </div>
        </div>
        """

    [<LitElement("modal-button")>]
    static member ModalButtonWebComponent() =
        Hook.useHmr(hmr)
        let _host, props = LitElement.init(fun config ->
            config.useShadowDom <- false
            config.props <- {| text = Prop.Of("I'm a modal") |}
        )
        let text = props.text.Value

        let active, setActive = Hook.useState(false)
        let className = Hook.use_scoped_css """
            .modal-content > .notification {
                margin: 0 auto;
                max-width: 500px;
            }
        """

        html $"""
        <div class={className}>
            <!-- button and modal -->
            <button class="button"
                @click={Ev(fun _ -> setActive true)}
                >
                Open modal
            </button>
            <div class={Lit.classes ["modal", true;  "is-active", active]}>
                <div class="modal-background"></div>
                <div class="modal-content">
                    <div class="notification">
                        {text}
                    </div>
                </div>
                <button class="modal-close is-large"
                    @click={Ev(fun _ -> setActive(false))}
                    ></button>
            </div>
        </div>
        """

    [<LitElement("shadow-button")>]
    static member ButtonShadowDom() =
        Hook.useHmr(hmr)
        let _host, props = LitElement.init(fun config ->
            config.props <- {| text = Prop.Of("I'm a button") |}
            config.styles <- [
                css $"""
                button {{
                    user-select: none;
                    cursor: pointer;
                    font-family: Arial, Helvetica, sans-serif;
                    font-size: medium;
                    background-color: white;
                    border-radius: 10px;
                    padding: 10px;
                    margin: 10px;
                }}

                button:hover {{
                    background-color: ghostwhite;
                }}

                button:hover:active {{
                    background-color: gainsboro;
                }}
                """
            ]
        )
        let count, setCount = Hook.useState(0)
        let text = $"{props.text.Value} ({count})"

        html $"""
        <button @click={Ev(fun _ -> count + 1 |> setCount)}>
            {text}
        </button>
        """
