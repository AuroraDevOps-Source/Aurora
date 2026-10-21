import fs from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';
const saved = new Map();
function fixture() {
 const listeners = {};
 const attributes = {};
 const style = {};
 const divider = { addEventListener: (type, fn) => { assert.equal(listeners[type], undefined); listeners[type] = fn; }, setAttribute: (k,v) => attributes[k]=v, setPointerCapture() {} };
 const grid = {querySelector: () => divider, style: {setProperty:(k,v)=>style[k]=v},getBoundingClientRect:()=>({left:100,width:1014})};
 return {listeners,attributes,style,grid};
}
let current=fixture();
const context={window:{},document:{getElementById:()=>current.grid},sessionStorage:{getItem:k=>saved.get(k)??null,setItem:(k,v)=>saved.set(k,v)}};
vm.runInNewContext(fs.readFileSync('src/Aurora.Client/wwwroot/js/map.js','utf8'),context);
context.window.routeMap.initSplitter('test');
context.window.routeMap.initSplitter('test');
assert.equal(current.style['--planner-split'],'0.62');
const fire=(type,args={})=>current.listeners[type]({preventDefault(){},...args});
fire('pointermove',{pointerId:1,clientX:357});assert.equal(current.style['--planner-split'],'0.62');
fire('pointerdown',{button:0,pointerId:1});fire('pointermove',{pointerId:1,clientX:507});assert.equal(current.style['--planner-split'],'0.4');
fire('pointermove',{pointerId:1,clientX:9999});assert.equal(current.style['--planner-split'],'0.75');
fire('pointerup');fire('pointermove',{pointerId:1,clientX:200});assert.equal(current.style['--planner-split'],'0.75');
fire('keydown',{key:'Home'});assert.equal(current.style['--planner-split'],'0.25');
fire('keydown',{key:'ArrowRight'});assert.equal(current.attributes['aria-valuenow'],'27');
current=fixture();context.window.routeMap.initSplitter('test');assert.equal(current.style['--planner-split'],'0.27');
fire('dblclick');assert.equal(current.style['--planner-split'],'0.62');
console.log('PASS: divider drag, bounds, release, keyboard, persistence, reset, and duplicate initialization.');
