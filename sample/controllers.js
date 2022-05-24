import { LitElement } from "lit";

export class MouseController {

    constructor(host) {
        this.host = host;
        host.addController(this);
        this.x = 0;
        this.y = 0;
    }

    _updatePosition =
        /**
         *
         * @param {MouseEvent} event
         */
        (event) => {
            this.x = event.x;
            this.y = event.y;
            this.host.requestUpdate();
        };

    hostConnected() {
        window.addEventListener('mousemove', this._updatePosition);
    }

    hostDisconnected() {
        window.removeEventListener('mousemove', this._updatePosition);
    }

}



class MyControlledElement extends LitElement {

    constructor() {
        super();
        this.mouseCtrl = new MouseController(this);
    }

    render() {
        return `Cursor position: ${this.mouseCtrl.x} - ${this.mouseCtrl.y}`;
    }

}

customElements.define("my-controlled-element", MyControlledElement);