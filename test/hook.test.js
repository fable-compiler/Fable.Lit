import { expect, fixture, aTimeout } from '@open-wc/testing';
import { html } from 'lit/static-html.js';
import { createRef } from 'lit/directives/ref.js';
import * as Components from './Hook.fs.js';

describe('Hook', () => {
    it('has a default value of 5', async () => {
        const el = await fixture(html`${Components.Counter()}`);
        expect(el.querySelector("p")).dom.text('Value: 5');
    });

    it('increases/decreases the counter on button click', async () => {
        const el = await fixture(html`${Components.Counter()}`);
        el.querySelector(".incr").click();
        expect(el.querySelector("p")).dom.text('Value: 6');
        el.querySelector(".decr").click();
        el.querySelector(".decr").click();
        expect(el.querySelector("p")).dom.text('Value: 4');
    });

    it('useEffectOnce runs on mount/dismount', async () => {
        const r = createRef();
        r.value = 8;

        const el = await fixture(html`${Components.DisposableContainer(r)}`);
        expect(el.querySelector("p")).dom.text('Value: 5');

        // Effect is run asynchronously after after render
        expect(r.value).to.equals(9);

        // Effect is not run again on rerenders
        el.querySelector(".incr").click();
        expect(el.querySelector("p")).dom.text('Value: 6');
        expect(r.value).to.equals(9);

        // Cause the component to be dismounted
        el.querySelector(".dispose").click();
        // Effect has been disposed
        expect(r.value).to.equals(19);
    });

    it("useMemo doesn't change without dependencies", async () => {
        const el = await fixture(html`${Components.MemoizedValue()}`);
        const state = el.querySelector("#state-btn");
        const second = el.querySelector("#second-btn");

        state.click();
        expect(el.querySelector("#state")).dom.text('11');
        // un-related re-renders shouldn't affect memoized value
        expect(el.querySelector("#memoized")).dom.text('1');
        // change second value trice
        second.click(); second.click(); second.click();

        // second should have changed
        expect(el.querySelector("#second")).dom.text('3');
        // memoized value should have not changed
        expect(el.querySelector("#memoized")).dom.text('1');
    });

    it("useElmish dispatches messages correctly", async () => {
        const el = await fixture(html`${Components.ElmishComponent()}`);
        const inc = el.querySelector("#inc");
        const decr = el.querySelector("#decr");
        const delayedReset = el.querySelector("#delay-reset");
        // normal dispatch works
        expect(el.querySelector("#count")).dom.text("0");
        inc.click(); inc.click();

        // normal dispatch works
        expect(el.querySelector("#count")).dom.text("2");
        decr.click(); decr.click();

        expect(el.querySelector("#count")).dom.text("0");
        decr.click(); decr.click();

        // normal dispatch works
        expect(el.querySelector("#count")).dom.text("-2");

        delayedReset.click();
        await aTimeout(500);
        // dispatch with async cmd works
        expect(el.querySelector("#count")).dom.text("0");
    });

});