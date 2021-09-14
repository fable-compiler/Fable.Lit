import { expect, fixture } from '@open-wc/testing';
import { html } from 'lit/static-html.js';
import { createRef } from 'lit-html/directives/ref.js';
import * as Components from './Hook.fs.js';

function sleep(ms = 0) {
    return new Promise(resolve => setTimeout(() => resolve(), ms));
}

describe('Hook', () => {
    it('has a default value of 5', async () => {
        const el = await fixture(html`${Components.Counter()}`);
        expect(el.querySelector("p")).dom.to.equal('<p>Value: 5</p>');
    });

    it('increases/decreases the counter on button click', async () => {
        const el = await fixture(html`${Components.Counter()}`);
        el.querySelector(".incr").click();
        expect(el.querySelector("p")).dom.to.equal('<p>Value: 6</p>');
        el.querySelector(".decr").click();
        el.querySelector(".decr").click();
        expect(el.querySelector("p")).dom.to.equal('<p>Value: 4</p>');
    });

    it('useEffectOnce runs on mount/dismount', async () => {
        const r = createRef();
        r.value = 8;

        const el = await fixture(html`${Components.DisposableContainer(r)}`);
        expect(el.querySelector("p")).dom.to.equal('<p>Value: 5</p>');

        // Effect is run asynchronously after after render
        expect(r.value).to.equals(9);

        // Effect is not run again on rerenders
        el.querySelector(".incr").click();
        expect(el.querySelector("p")).dom.to.equal('<p>Value: 6</p>');
        expect(r.value).to.equals(9);

        // Cause the component to be dismounted
        el.querySelector(".dispose").click();
        // Effect has been disposed
        expect(r.value).to.equals(19);
    });

});