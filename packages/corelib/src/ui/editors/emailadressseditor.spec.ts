import { EmailAddressEditor } from "./emailaddresseditor";

describe("EmailAdressEditor", () => {

    it("sets input type correctly", () => {
        var editor = new EmailAddressEditor($("<input type='text'/>"));
        expect(editor.element.attr("type")).toEqual("email");
    });

});