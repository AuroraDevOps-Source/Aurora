import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';

const markers = [], routeLayers = [], maps = [], lines = [];
let routeLineCount = 0;
const layerObject = () => ({ addTo(target) { target.layers.add(this); return this; }, bindPopup(text) { this.popup = text; return this; } });
const context = {
    window: {}, setTimeout: action => action(),
    document: { getElementById: () => ({}), createElement: () => ({ set textContent(value) { this.innerHTML = String(value).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;'); } }) },
    L: {
        map: () => { const map = { layers: new Set(), removeLayer(layer) { this.layers.delete(layer); }, fitBounds(bounds) { this.bounds = bounds; }, setView() {}, invalidateSize() {}, remove() {} }; maps.push(map); return map; },
        control: { zoom: () => layerObject() }, tileLayer: () => layerObject(),
        layerGroup: () => { const layer = { ...layerObject(), layers: new Set() }; routeLayers.push(layer); return layer; },
        polyline: points => { routeLineCount++; const line = { ...layerObject(), points, setLatLngs(value) { this.points = value; } }; lines.push(line); return line; }, divIcon: options => options,
        marker: (point, options) => { const marker = { ...layerObject(), point, options }; markers.push(marker); return marker; },
        latLngBounds: points => ({ points, isValid: () => points.length > 0 })
    }
};
context.window.L = context.L;
vm.runInNewContext(readFileSync(new URL('../src/Aurora.Client/wwwroot/js/map.js', import.meta.url), 'utf8'), context);
context.window.routeMap.render('map', {
    routes: ['A', 'B'].map((vehicle, index) => ({ vehicle, color: '#2563eb', stops: [{ lat: 39 + index, lon: -94, locationId: '<script>bad</script>', orderIds: ['PRO'], arrival: '9:00 AM', departure: '9:15 AM' }] })),
    dropped: [{ orderId: 'LEFT_1', latitude: 39, longitude: -95, reason: '<b>reason</b>' }, { orderId: 'LEFT_2', latitude: 39, longitude: -95, reason: 'Window' }]
});
assert.equal(maps.length, 1);
assert.equal(markers.length, 3, 'overlapping red orders share one counted marker');
assert.match(markers[2].options.icon.html, />2</);
assert.match(markers[2].popup, /LEFT_1.*LEFT_2/);
assert.match(markers[0].popup, /&lt;script&gt;/, 'popup metadata is escaped');
context.window.routeMap.filter('A');
assert.equal(maps[0].layers.has(routeLayers[0]), true);
assert.equal(maps[0].layers.has(routeLayers[1]), false);
assert.equal(maps[0].layers.has(markers[2]), true, 'unscheduled marker remains visible');
assert.equal(maps[0].bounds.points.length, 1, 'fit focuses selected route');
context.window.routeMap.filter(null);
assert.equal(maps[0].layers.has(routeLayers[1]), true, 'show all restores hidden route');
assert.equal(maps[0].bounds.points.length, 3);
assert.equal(maps.length, 1, 'filter never rebuilds the map or submits another optimization');
context.window.routeMap.dispose();
context.window.routeMap.filter('A');
console.log('12 map checks passed: filtering, restoration, persistent red markers, overlap counts, safe popups, and disposal.');

const previousMarkerCount = markers.length, previousLineCount = routeLineCount;
context.window.routeMap.preview('map', [
    { latitude: 39, longitude: -94, id: 'PRO_A', label: '<img src=x>', isDepot: false },
    { latitude: 39, longitude: -94, id: 'PRO_B', label: 'Same address', isDepot: false },
    { latitude: 38, longitude: -94, id: 'DEPOT', label: 'Depot', isDepot: true },
    { latitude: 999, longitude: -94, id: 'BAD', label: 'Invalid', isDepot: false }
]);
assert.equal(markers.length - previousMarkerCount, 2, 'preview groups deliveries and excludes invalid coordinates');
assert.equal(routeLineCount, previousLineCount, 'preview never invents optimized route lines');
assert.match(markers[previousMarkerCount].options.icon.html, />2</);
assert.match(markers[previousMarkerCount].popup, /Unassigned delivery location/);
assert.match(markers[previousMarkerCount].popup, /&lt;img src=x&gt;/);
assert.match(markers[previousMarkerCount + 1].options.icon.html, /depot/);
assert.equal(maps.at(-1).bounds.points.length, 2, 'preview bounds use actual input points');
context.window.routeMap.preview('map', []);
assert.equal(routeLineCount, previousLineCount, 'empty workspace does not render fake routes');
console.log('8 additional input-preview map checks passed.');

context.window.routeMap.render('map', {
    routes: ['A', 'B'].map(vehicle => ({ vehicle, color: '#2563eb', stops: [{ lat: 39, lon: -94 }, { lat: 40, lon: -95 }] })),
    dropped: [{ orderId: 'LEFT', latitude: 38, longitude: -96 }]
});
const roadMap = maps.at(-1), firstLine = lines.at(-2), secondLine = lines.at(-1);
const originalPoints = JSON.stringify(firstLine.points), markerCount = markers.length, mapCount = maps.length;
context.window.routeMap.setRoadRoute('A', [{ latitude: 39, longitude: -94 }, { latitude: 39.7, longitude: -94.6 }, { latitude: 40, longitude: -95 }]);
context.window.routeMap.filter('A');
context.window.routeMap.setMode(true);
assert.equal(firstLine.points.length, 3, 'road mode uses the returned road geometry');
assert.equal(secondLine.points.length, 0, 'unloaded roads never appear as straight-line directions');
assert.equal(roadMap.bounds.points.length, 3, 'fit includes road bends');
assert.equal(roadMap.layers.has(routeLayers.at(-1)), false, 'toggle preserves manifest selection');
assert.equal(roadMap.layers.has(markers.at(-1)), true, 'road mode retains unscheduled markers');
context.window.routeMap.setMode(false);
assert.equal(JSON.stringify(firstLine.points), originalPoints, 'bird’s-eye restores original stop sequence');
assert.equal(secondLine.points.length, 2, 'bird’s-eye restores unloaded routes');
context.window.routeMap.setMode(true);
assert.equal(firstLine.points.length, 3, 'road geometry is reused across toggles');
assert.equal(markers.length, markerCount, 'toggle preserves every marker');
assert.equal(maps.length, mapCount, 'toggle does not rebuild map');
context.window.routeMap.dispose();
context.window.routeMap.setRoadRoute('A', []);
context.window.routeMap.setMode(true);
console.log('12 road-view checks passed: geometry, bounds, toggles, caching, filtering, and disposal.');
