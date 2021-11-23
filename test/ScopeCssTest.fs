module ScopeCssTest

open Expect
open Expect.Dom
open WebTestRunner
open Lit

module Expect =
    let equal (s1: string) (s2: string) =
        Expect.equal (s1.Trim()) (s2.Trim())

describe "Scope CSS" <| fun () ->

    itSync "scopes css" <| fun () ->
        """
.settings-panel-label {font-size: 13px}
.settings-panel-control {font-size: 11px;}
        """
        |> Parser.Css.scope "settings-panel"
        |> Expect.equal """
.settings-panel .settings-panel-label {font-size: 13px}
.settings-panel .settings-panel-control {font-size: 11px;}
        """

    itSync "replaces :host" <| fun () ->
        """
.settings-panel-label {font-size: var(--font-size);}
:host {font-size: 11px;}
        """
        |> Parser.Css.scope "settings-panel"
        |> Expect.equal """
.settings-panel .settings-panel-label {font-size: var(--font-size);}
.settings-panel {font-size: 11px;}
        """

    itSync "works with multiple selectors" <| fun () ->
        """
        .header > .title, .header + .title, .header ~ .title {
            color:#444;
        }
        """
        |> Parser.Css.scope "xyz42"
        |> Expect.equal """
        .xyz42 .header > .title, .xyz42 .header + .title, .xyz42 .header ~ .title {
            color:#444;
        }
        """

    itSync "handles keyframes" <| fun () ->
        """
@keyframes loading {
    from {background: red;}
    50%, +60.8%, -.3% {background: yellow;}
    100% {background: green;}
}
@keyframes loading-2 {}
.anim1 {
    animation: infinite loading 4s;
}
.anim2 {
    animation-name: loading-2;
}
    """
        |> Parser.Css.scope "xyz42"
        |> Expect.equal """
@keyframes xyz42-loading {
    from {background: red;}
    50%, +60.8%, -.3% {background: yellow;}
    100% {background: green;}
}
@keyframes xyz42-loading-2 {}
.xyz42 .anim1 {
    animation: infinite xyz42-loading 4s;
}
.xyz42 .anim2 {
    animation-name: xyz42-loading-2;
}
        """

    itSync "curly braces with strings" <| fun () ->
        """.foo {content:"\"}"} .bar { opacity: 0 }"""
        |> Parser.Css.scope "test"
        |> Expect.equal """.test .foo {content:"\"}"} .test .bar { opacity: 0 }"""

    itSync "block comments" <| fun () ->
        """
/* Default triangle styles are from control theme, just set display: block */
.settings-panel-select-triangle {
		position: absolute;
		border-right: .3em solid transparent;
		border-left: .3em solid transparent;
		right: 2.5%;
		height: 0;
		z-index: 1;
		pointer-events: none;
	}
        """
        |> Parser.Css.scope "x"
        |> Expect.equal """
.x .settings-panel-select-triangle {
		position: absolute;
		border-right: .3em solid transparent;
		border-left: .3em solid transparent;
		right: 2.5%;
		height: 0;
		z-index: 1;
		pointer-events: none;
	}
        """

    itSync "directives" <| fun () ->
        """
@supports (--css: variables) {
    body {
        --track-background: linear-gradient(to right, var(--active) 0, var(--active) var(--value), var(--bg) 0) no-repeat;
    }
}
@import url("chrome://communicator/skin/") screen;
@media (min-width: 440px) {
    tag {--x: 1}
}
@font-face {
    font-family: "Bitstream Vera Serif Bold";
    src: url("https://mdn.mozillademos.org/files/2468/VeraSeBd.ttf");
}
        """
        |> Parser.Css.scope "x"
        |> Expect.equal """
@supports (--css: variables) {
    .x body {
        --track-background: linear-gradient(to right, var(--active) 0, var(--active) var(--value), var(--bg) 0) no-repeat;
    }
}
@import url("chrome://communicator/skin/") screen;
@media (min-width: 440px) {
    .x tag {--x: 1}
}
@font-face {
    font-family: "Bitstream Vera Serif Bold";
    src: url("https://mdn.mozillademos.org/files/2468/VeraSeBd.ttf");
}
        """

    itSync "real use-case" <| fun () ->
        """
.clearfix{*zoom:1;}.clearfix:before,.clearfix:after{display:table;content:\"\";line-height:0;}
.clearfix:after{clear:both;}
.hide-text{font:0/0 a;color:transparent;text-shadow:none;background-color:transparent;border:0;}
.input-block-level{display:block;width:100%;min-height:30px;-webkit-box-sizing:border-box;-moz-box-sizing:border-box;box-sizing:border-box;}
p{margin:0 0 10px;}
.lead{margin-bottom:20px;font-size:21px;font-weight:200;line-height:30px;}
small{font-size:85%;}
strong{font-weight:bold;}
em{font-style:italic;}
cite{font-style:normal;}
.muted{color:#999999;}
.text-warning{color:#c09853;}
.text-error{color:#b94a48;}
.text-info{color:#3a87ad;}
.text-success{color:#468847;}
h1,h2,h3,h4,h5,h6{margin:10px 0;font-family:inherit;font-weight:bold;line-height:1;color:inherit;text-rendering:optimizelegibility;}h1 small,h2 small,h3 small,h4 small,h5 small,h6 small{font-weight:normal;line-height:1;color:#999999;}
h1{font-size:36px;line-height:40px;}
h2{font-size:30px;line-height:40px;}
h3{font-size:24px;line-height:40px;}
h4{font-size:18px;line-height:20px;}
h5{font-size:14px;line-height:20px;}
h6{font-size:12px;line-height:20px;}
h1 small{font-size:24px;}
h2 small{font-size:18px;}
h3 small{font-size:14px;}
h4 small{font-size:14px;}
.page-header{padding-bottom:9px;margin:20px 0 30px;border-bottom:1px solid #eeeeee;}
ul,ol{padding:0;margin:0 0 10px 25px;}
ul ul,ul ol,ol ol,ol ul{margin-bottom:0;}
li{line-height:20px;}
ul.unstyled,ol.unstyled{margin-left:0;list-style:none;}
dl{margin-bottom:20px;}
dt,dd{line-height:20px;}
dt{font-weight:bold;}
dd{margin-left:10px;}
.dl-horizontal{*zoom:1;}.dl-horizontal:before,.dl-horizontal:after{display:table;content:\"\";line-height:0;}
.dl-horizontal:after{clear:both;}
.dl-horizontal dt{float:left;width:160px;clear:left;text-align:right;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
.dl-horizontal dd{margin-left:180px;}
hr{margin:20px 0;border:0;border-top:1px solid #eeeeee;border-bottom:1px solid #ffffff;}
abbr[title]{cursor:help;border-bottom:1px dotted #999999;}
abbr.initialism{font-size:90%;text-transform:uppercase;}
blockquote{padding:0 0 0 15px;margin:0 0 20px;border-left:5px solid #eeeeee;}blockquote p{margin-bottom:0;font-size:16px;font-weight:300;line-height:25px;}
blockquote small{display:block;line-height:20px;color:#999999;}blockquote small:before{content:\'\\2014 \\00A0\';}
blockquote.pull-right{float:right;padding-right:15px;padding-left:0;border-right:5px solid #eeeeee;border-left:0;}blockquote.pull-right p,blockquote.pull-right small{text-align:right;}
blockquote.pull-right small:before{content:\'\';}
blockquote.pull-right small:after{content:\'\\00A0 \\2014\';}
q:before,q:after,blockquote:before,blockquote:after{content:\"\";}
address{display:block;margin-bottom:20px;font-style:normal;line-height:20px;}
.label,.badge{font-size:11.844px;font-weight:bold;line-height:14px;color:#ffffff;vertical-align:baseline;white-space:nowrap;text-shadow:0 -1px 0 rgba(0, 0, 0, 0.25);background-color:#999999;}
.label{padding:1px 4px 2px;-webkit-border-radius:3px;-moz-border-radius:3px;border-radius:3px;}
.badge{padding:1px 9px 2px;-webkit-border-radius:9px;-moz-border-radius:9px;border-radius:9px;}
a.label:hover,a.badge:hover{color:#ffffff;text-decoration:none;cursor:pointer;}
.label-important,.badge-important{background-color:#b94a48;}
.label-important[href],.badge-important[href]{background-color:#953b39;}
.label-warning,.badge-warning{background-color:#f89406;}
.label-warning[href],.badge-warning[href]{background-color:#c67605;}
.label-success,.badge-success{background-color:#468847;}
.label-success[href],.badge-success[href]{background-color:#356635;}
.label-info,.badge-info{background-color:#3a87ad;}
.label-info[href],.badge-info[href]{background-color:#2d6987;}
.label-inverse,.badge-inverse{background-color:#333333;}
.label-inverse[href],.badge-inverse[href]{background-color:#1a1a1a;}
.btn .label,.btn .badge{position:relative;top:-1px;}
.btn-mini .label,.btn-mini .badge{top:0;}
form{margin:0 0 20px;}
fieldset{padding:0;margin:0;border:0;}
legend{display:block;width:100%;padding:0;margin-bottom:20px;font-size:21px;line-height:40px;color:#333333;border:0;border-bottom:1px solid #e5e5e5;}legend small{font-size:15px;color:#999999;}
label,input,button,select,textarea{font-size:14px;font-weight:normal;line-height:20px;}
input,button,select,textarea{font-family:\"Helvetica Neue\",Helvetica,Arial,sans-serif;}
label{display:block;margin-bottom:5px;}
select,textarea,input[type=\"text\"],input[type=\"password\"],input[type=\"datetime\"],input[type=\"datetime-local\"],input[type=\"date\"],input[type=\"month\"],input[type=\"time\"],input[type=\"week\"],input[type=\"number\"],input[type=\"email\"],input[type=\"url\"],input[type=\"search\"],input[type=\"tel\"],input[type=\"color\"],.uneditable-input{display:inline-block;height:20px;padding:4px 6px;margin-bottom:9px;font-size:14px;line-height:20px;color:#555555;-webkit-border-radius:3px;-moz-border-radius:3px;border-radius:3px;}
        """
        |> Parser.Css.scope "bootstrap"
        |> Expect.equal """
.bootstrap .clearfix{*zoom:1;}.bootstrap .clearfix:before,.bootstrap .clearfix:after{display:table;content:\"\";line-height:0;}
.bootstrap .clearfix:after{clear:both;}
.bootstrap .hide-text{font:0/0 a;color:transparent;text-shadow:none;background-color:transparent;border:0;}
.bootstrap .input-block-level{display:block;width:100%;min-height:30px;-webkit-box-sizing:border-box;-moz-box-sizing:border-box;box-sizing:border-box;}
.bootstrap p{margin:0 0 10px;}
.bootstrap .lead{margin-bottom:20px;font-size:21px;font-weight:200;line-height:30px;}
.bootstrap small{font-size:85%;}
.bootstrap strong{font-weight:bold;}
.bootstrap em{font-style:italic;}
.bootstrap cite{font-style:normal;}
.bootstrap .muted{color:#999999;}
.bootstrap .text-warning{color:#c09853;}
.bootstrap .text-error{color:#b94a48;}
.bootstrap .text-info{color:#3a87ad;}
.bootstrap .text-success{color:#468847;}
.bootstrap h1,.bootstrap h2,.bootstrap h3,.bootstrap h4,.bootstrap h5,.bootstrap h6{margin:10px 0;font-family:inherit;font-weight:bold;line-height:1;color:inherit;text-rendering:optimizelegibility;}.bootstrap h1 small,.bootstrap h2 small,.bootstrap h3 small,.bootstrap h4 small,.bootstrap h5 small,.bootstrap h6 small{font-weight:normal;line-height:1;color:#999999;}
.bootstrap h1{font-size:36px;line-height:40px;}
.bootstrap h2{font-size:30px;line-height:40px;}
.bootstrap h3{font-size:24px;line-height:40px;}
.bootstrap h4{font-size:18px;line-height:20px;}
.bootstrap h5{font-size:14px;line-height:20px;}
.bootstrap h6{font-size:12px;line-height:20px;}
.bootstrap h1 small{font-size:24px;}
.bootstrap h2 small{font-size:18px;}
.bootstrap h3 small{font-size:14px;}
.bootstrap h4 small{font-size:14px;}
.bootstrap .page-header{padding-bottom:9px;margin:20px 0 30px;border-bottom:1px solid #eeeeee;}
.bootstrap ul,.bootstrap ol{padding:0;margin:0 0 10px 25px;}
.bootstrap ul ul,.bootstrap ul ol,.bootstrap ol ol,.bootstrap ol ul{margin-bottom:0;}
.bootstrap li{line-height:20px;}
.bootstrap ul.unstyled,.bootstrap ol.unstyled{margin-left:0;list-style:none;}
.bootstrap dl{margin-bottom:20px;}
.bootstrap dt,.bootstrap dd{line-height:20px;}
.bootstrap dt{font-weight:bold;}
.bootstrap dd{margin-left:10px;}
.bootstrap .dl-horizontal{*zoom:1;}.bootstrap .dl-horizontal:before,.bootstrap .dl-horizontal:after{display:table;content:\"\";line-height:0;}
.bootstrap .dl-horizontal:after{clear:both;}
.bootstrap .dl-horizontal dt{float:left;width:160px;clear:left;text-align:right;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;}
.bootstrap .dl-horizontal dd{margin-left:180px;}
.bootstrap hr{margin:20px 0;border:0;border-top:1px solid #eeeeee;border-bottom:1px solid #ffffff;}
.bootstrap abbr[title]{cursor:help;border-bottom:1px dotted #999999;}
.bootstrap abbr.initialism{font-size:90%;text-transform:uppercase;}
.bootstrap blockquote{padding:0 0 0 15px;margin:0 0 20px;border-left:5px solid #eeeeee;}.bootstrap blockquote p{margin-bottom:0;font-size:16px;font-weight:300;line-height:25px;}
.bootstrap blockquote small{display:block;line-height:20px;color:#999999;}.bootstrap blockquote small:before{content:\'\\2014 \\00A0\';}
.bootstrap blockquote.pull-right{float:right;padding-right:15px;padding-left:0;border-right:5px solid #eeeeee;border-left:0;}.bootstrap blockquote.pull-right p,.bootstrap blockquote.pull-right small{text-align:right;}
.bootstrap blockquote.pull-right small:before{content:\'\';}
.bootstrap blockquote.pull-right small:after{content:\'\\00A0 \\2014\';}
.bootstrap q:before,.bootstrap q:after,.bootstrap blockquote:before,.bootstrap blockquote:after{content:\"\";}
.bootstrap address{display:block;margin-bottom:20px;font-style:normal;line-height:20px;}
.bootstrap .label,.bootstrap .badge{font-size:11.844px;font-weight:bold;line-height:14px;color:#ffffff;vertical-align:baseline;white-space:nowrap;text-shadow:0 -1px 0 rgba(0, 0, 0, 0.25);background-color:#999999;}
.bootstrap .label{padding:1px 4px 2px;-webkit-border-radius:3px;-moz-border-radius:3px;border-radius:3px;}
.bootstrap .badge{padding:1px 9px 2px;-webkit-border-radius:9px;-moz-border-radius:9px;border-radius:9px;}
.bootstrap a.label:hover,.bootstrap a.badge:hover{color:#ffffff;text-decoration:none;cursor:pointer;}
.bootstrap .label-important,.bootstrap .badge-important{background-color:#b94a48;}
.bootstrap .label-important[href],.bootstrap .badge-important[href]{background-color:#953b39;}
.bootstrap .label-warning,.bootstrap .badge-warning{background-color:#f89406;}
.bootstrap .label-warning[href],.bootstrap .badge-warning[href]{background-color:#c67605;}
.bootstrap .label-success,.bootstrap .badge-success{background-color:#468847;}
.bootstrap .label-success[href],.bootstrap .badge-success[href]{background-color:#356635;}
.bootstrap .label-info,.bootstrap .badge-info{background-color:#3a87ad;}
.bootstrap .label-info[href],.bootstrap .badge-info[href]{background-color:#2d6987;}
.bootstrap .label-inverse,.bootstrap .badge-inverse{background-color:#333333;}
.bootstrap .label-inverse[href],.bootstrap .badge-inverse[href]{background-color:#1a1a1a;}
.bootstrap .btn .label,.bootstrap .btn .badge{position:relative;top:-1px;}
.bootstrap .btn-mini .label,.bootstrap .btn-mini .badge{top:0;}
.bootstrap form{margin:0 0 20px;}
.bootstrap fieldset{padding:0;margin:0;border:0;}
.bootstrap legend{display:block;width:100%;padding:0;margin-bottom:20px;font-size:21px;line-height:40px;color:#333333;border:0;border-bottom:1px solid #e5e5e5;}.bootstrap legend small{font-size:15px;color:#999999;}
.bootstrap label,.bootstrap input,.bootstrap button,.bootstrap select,.bootstrap textarea{font-size:14px;font-weight:normal;line-height:20px;}
.bootstrap input,.bootstrap button,.bootstrap select,.bootstrap textarea{font-family:\"Helvetica Neue\",Helvetica,Arial,sans-serif;}
.bootstrap label{display:block;margin-bottom:5px;}
.bootstrap select,.bootstrap textarea,.bootstrap input[type=\"text\"],.bootstrap input[type=\"password\"],.bootstrap input[type=\"datetime\"],.bootstrap input[type=\"datetime-local\"],.bootstrap input[type=\"date\"],.bootstrap input[type=\"month\"],.bootstrap input[type=\"time\"],.bootstrap input[type=\"week\"],.bootstrap input[type=\"number\"],.bootstrap input[type=\"email\"],.bootstrap input[type=\"url\"],.bootstrap input[type=\"search\"],.bootstrap input[type=\"tel\"],.bootstrap input[type=\"color\"],.bootstrap .uneditable-input{display:inline-block;height:20px;padding:4px 6px;margin-bottom:9px;font-size:14px;line-height:20px;color:#555555;-webkit-border-radius:3px;-moz-border-radius:3px;border-radius:3px;}
        """
