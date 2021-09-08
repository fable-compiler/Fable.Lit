import { expect } from '@open-wc/testing';
import { html, render } from 'lit-html';
import { createRef } from 'lit-html/directives/ref.js';
import * as Components from './Hook.fs.js';

// There's a `fixture` helper in @open-wc/testing to render lit templates
// but it doesn't seem to work with lit-html 2
/**
 * @param {{ (el: HTMLElement): Promise<void> }} f
 */
async function withEl(f) {
    const div = document.createElement("div");
    document.body.append(div);
    await f(div);
    document.body.removeChild(div);
}

function sleep(ms = 0) {
    return new Promise(resolve => setTimeout(() => resolve()), ms);
}

describe('Hook', () => {
    it('has a default value of 5', () => withEl(el => {
        render(Components.Counter(), el);
        expect(el.querySelector("p")).dom.to.equal('<p>Value: 5</p>');
    }));

    it('increases/decreases the counter on button click', () => withEl(el => {
        render(Components.Counter(), el);
        el.querySelector(".incr").click();
        expect(el.querySelector("p")).dom.to.equal('<p>Value: 6</p>');
        el.querySelector(".decr").click();
        el.querySelector(".decr").click();
        expect(el.querySelector("p")).dom.to.equal('<p>Value: 4</p>');
    }));

    it('useEffectOnce runs on mount/dismount', () => withEl(async el => {
        const r = createRef();
        r.value = 8;

        render(Components.Disposable(r), el);
        expect(el.querySelector("p")).dom.to.equal('<p>Value: 5</p>');

        // Effect is run asynchronously after after render
        expect(r.value).to.equals(8);
        await sleep();
        expect(r.value).to.equals(9);

        // Effect is not run again on rerenders
        el.querySelector(".incr").click();
        expect(el.querySelector("p")).dom.to.equal('<p>Value: 6</p>');
        expect(r.value).to.equals(9);

        // Cause the component to be dismounted
        render(html`<div></div>`, el);

        // Effect has been disposed
        expect(r.value).to.equals(19);
    }));

});