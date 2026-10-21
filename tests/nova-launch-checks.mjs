import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
const script=fs.readFileSync('src/Aurora.Client/wwwroot/js/login.js','utf8');
const db='11111111-1111-1111-1111-111111111111';
const ticket='a'.repeat(64);
function run(hash) {
    const store=new Map([['oidc.user:https://aurora.invalid:aurora-spa','old-user'],['unrelated','keep']]);
    const storage={get length(){return store.size;},key:i=>[...store.keys()][i],removeItem:k=>store.delete(k)};
    let replaced;
    const window={location:{pathname:'/nova-launch',hash},history:{replaceState:(_,__,url)=>replaced=url},localStorage:storage,sessionStorage:storage};
    vm.runInNewContext(script,{window,URLSearchParams});
    assert.equal(replaced,'/nova-launch');
    assert.equal(store.has('oidc.user:https://aurora.invalid:aurora-spa'),false);
    assert.equal(store.get('unrelated'),'keep');
    return window.auroraLogin;
}
const helper=run(`#db=${db}&ticket=${ticket}`);
assert.equal(helper.takeNovaLaunch().ticket,ticket);
assert.equal(helper.takeNovaLaunch(),null);
for(const fragment of ['',`#db=${db}`,`#db=bad&ticket=${ticket}`,`#db=${db}&ticket=${'A'.repeat(64)}`])
    assert.equal(run(fragment).takeNovaLaunch(),null);
console.log('PASS Nova launch fragment removal, single read, old-user cleanup, unrelated storage preservation, invalid launch rejection.');

const codeHelper=run(`#code=${ticket}`);
assert.equal(codeHelper.takeNovaLaunch().code,ticket);
assert.equal(codeHelper.takeNovaLaunch(),null);
assert.equal(run('#code=invalid').takeNovaLaunch(),null);
assert.equal(run(`#code=invalid&db=${db}&ticket=${ticket}`).takeNovaLaunch(),null);
console.log('PASS API launch code capture, one-time read, and malformed code rejection.');
