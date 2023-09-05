// jquery validate uses require to access jquery when it sees modular.
jest.mock("jquery", () => {
    return $;
})

import { validateForm } from "@serenity-is/corelib/q";
import { EmailEditor } from "./emaileditor";
import v from "@optionaldeps/jquery.validation";

[v]
describe("EmailEditor", () => {

    it("validates invalid email addresses", async () => {
        var form = $("<form/>");
        var editor = new EmailEditor($('<input name="test" type="text"/>').appendTo(form), {});
        editor.element.toggleClass('required', true);
        editor.element.val("*-z");
        var validator = validateForm(form, {debug: true, ignore: ''});
        validator.checkForm();
        expect(validator.errorList.length).toBe(1);
        expect(validator.errorList[0].message).toBe("Please enter a valid email address.");
    });

});
