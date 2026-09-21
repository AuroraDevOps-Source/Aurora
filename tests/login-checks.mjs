import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

class HTMLFormElement {
    id = 'aurora-login-form';
    values = new Map();
}
class FormData {
    constructor(form) { this.values = form.values; }
    get(name) { return this.values.get(name) ?? null; }
}
const context = { window: {}, HTMLFormElement, FormData };
vm.runInNewContext(fs.readFileSync('src/Aurora.Client/wwwroot/js/login.js', 'utf8'), context);
const read = context.window.auroraLogin.readCredentials;
const form = new HTMLFormElement();
assert.equal(read(form).userName, '');
assert.equal(read(form).password, '');
form.values.set('username', ' demo@example.invalid ');
form.values.set('password', ' synthetic password with spaces ');
assert.equal(read(form).userName, 'demo@example.invalid');
assert.equal(read(form).password, ' synthetic password with spaces ');
// Simulate a password manager changing DOM values without sending input/change events.
form.values.set('username', 'autofilled@example.invalid');
form.values.set('password', 'autofilled synthetic value');
assert.equal(read(form).userName, 'autofilled@example.invalid');
assert.equal(read(form).password, 'autofilled synthetic value');
assert.throws(() => read({}), /Expected the Aurora sign-in form/);
form.id = 'unrelated-form';
assert.throws(() => read(form), /Expected the Aurora sign-in form/);

const page = fs.readFileSync('src/Aurora.Client/Pages/Login.razor', 'utf8');
assert.match(page, /<form[^>]*id="aurora-login-form"/);
assert.match(page, /name="username" type="text" autocomplete="username"/);
assert.match(page, /name="password" type="password" autocomplete="current-password"/);
assert.match(page, /ButtonType="ButtonType.Submit"/);
assert.doesNotMatch(page, /OnInitializedAsync|rememberMe|isPersistent/i);
console.log('13 login checks passed: native form metadata, submit handling, autofilled values, password whitespace, form isolation, and unchanged session behavior.');

form.id = 'aurora-login-form';
form.values.set('username', ' bbatts ');
assert.equal(read(form).userName, 'bbatts');
assert.match(page, /<label for="aurora-username">Username<\/label>/);
assert.doesNotMatch(page, /type="email"|Invalid email|Enter your email/);
console.log('3 additional username-login checks passed.');
