import { expect, fixture } from '@open-wc/testing';
import { html } from 'lit/static-html.js';
import './LitElement.fs.js';

describe('LitElement', () => {
    it("Renders", async () => {
        const el = await fixture(html`<fable-element></fable-element>`);
        expect(el.shadowRoot).to.not.be.undefined;
        expect(el.shadowRoot).to.not.be.null;

        expect(el.shadowRoot.querySelector("p")).dom.text("Element");

    });

    it("Reacts to attribute/property changes", async () => {
        const el = await fixture(html`<fel-attribute-changes></fel-attribute-changes>`);
        // check the default value
        expect(el.shadowRoot.querySelector("#value")).dom.text("default");
        el.setAttribute("f-name", "fable");
        // wait for lit's render updates
        await el.updateComplete;
        expect(el.shadowRoot.querySelector("#value")).dom.text("fable");
        // update property manually
        el.fName = "fable-2";
        await el.updateComplete;
        expect(el.shadowRoot.querySelector("#value")).dom.text("fable-2");
    });

    it("Doesn't react to attribute changes", async () => {
        const el = await fixture(html`<fel-attribute-doesnt-change></fel-attribute-doesnt-change>`);
        // check the default value
        expect(el.shadowRoot.querySelector("#value")).dom.text("default");
        el.setAttribute("f-name", "fable");
        // wait for lit's render updates
        await el.updateComplete;
        //
        expect(el.shadowRoot.querySelector("#value")).dom.text("default");
        el.fName = "fable";
        // wait for lit's render updates
        await el.updateComplete;
        expect(el.shadowRoot.querySelector("#value")).dom.text("fable");
    });

    it("Reflect Attribute changes", async () => {
        const el = await fixture(html`<fel-attribute-reflects></fel-attribute-reflects>`);
        // check the default value
        expect(el.shadowRoot.querySelector("#value")).dom.text("default");
        // the default value should have updated the attribute
        expect(el.getAttribute("f-name")).to.be.equal("default");
        el.fName = "fable";
        // wait for lit's render updates
        await el.updateComplete;
        // setting the property should have updated the value and the attribute
        expect(el.shadowRoot.querySelector("#value")).dom.text("fable");
        expect(el.getAttribute("f-name")).to.be.equal("fable");
    });
});


