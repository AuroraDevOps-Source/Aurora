window.routeMap = (() => {
    let map;
    let bounds;
    let routes = [];
    let droppedPoints = [];
    let roadMode = false;
    let selectedVehicle = null;

    function dispose() {
        if (map) map.remove();
        map = null;
        bounds = null;
        routes = [];
        droppedPoints = [];
        roadMode = false;
        selectedVehicle = null;
    }

    function escapeHtml(value) {
        const div = document.createElement('div');
        div.textContent = value ?? '';
        return div.innerHTML;
    }

    function initialize(elementId) {
        dispose();
        const element = document.getElementById(elementId);
        if (!element || !window.L) return false;

        map = L.map(element, { zoomControl: false, attributionControl: true });
        L.control.zoom({ position: 'bottomright' }).addTo(map);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; OpenStreetMap contributors'
        }).addTo(map);
        return true;
    }

    function preview(elementId, inputPoints) {
        if (!initialize(elementId)) return;
        const groups = new Map();
        (inputPoints || []).forEach(point => {
            if (!Number.isFinite(point.latitude) || !Number.isFinite(point.longitude) || Math.abs(point.latitude) > 90 || Math.abs(point.longitude) > 180) return;
            const key = `${point.isDepot}:${point.latitude}:${point.longitude}`;
            if (!groups.has(key)) groups.set(key, []);
            groups.get(key).push(point);
        });
        const points = [];
        groups.forEach(group => {
            const first = group[0], point = [first.latitude, first.longitude];
            points.push(point);
            const icon = L.divIcon({ className: '', html: `<span class="map-preview-pin ${first.isDepot ? 'depot' : ''}">${first.isDepot ? 'D' : group.length > 1 ? group.length : '•'}</span>`, iconSize: [28, 28], iconAnchor: [14, 14] });
            L.marker(point, { icon }).addTo(map).bindPopup(
                `<strong>${first.isDepot ? 'Depot' : 'Unassigned delivery location'}</strong><br>` +
                group.slice(0, 10).map(p => `${escapeHtml(p.id)} · ${escapeHtml(p.label)}`).join('<br>') +
                (group.length > 10 ? `<br>and ${group.length - 10} more` : ''));
        });
        bounds = points.length ? L.latLngBounds(points) : null;
        fit();
        setTimeout(() => map?.invalidateSize(), 50);
    }

    function render(elementId, data) {
        if (!initialize(elementId)) return;

        const allPoints = [];
        (data.routes || []).forEach(route => {
            const layer = L.layerGroup().addTo(map);
            const points = (route.stops || []).map(stop => [stop.lat, stop.lon]);
            const entry = { vehicle: route.vehicle, layer, points, roadPoints: null, line: null };
            routes.push(entry);
            if (points.length > 1) {
                entry.line = L.polyline(points, { color: route.color, weight: 4, opacity: .86 }).addTo(layer);
            }
            (route.stops || []).forEach((stop, index) => {
                const point = [stop.lat, stop.lon];
                allPoints.push(point);
                const marker = L.marker(point, {
                    icon: L.divIcon({
                        className: '',
                        html: stop.isDepot
                            ? `<span class="map-depot" style="--route:${route.color}">D</span>`
                            : `<span class="map-stop" style="--route:${route.color}">${index}</span>`,
                        iconSize: [28, 28], iconAnchor: [14, 14]
                    })
                }).addTo(layer);
                const orders = (stop.orderIds || []).length ? stop.orderIds.map(escapeHtml).join(', ') : 'No delivery';
                marker.bindPopup(`<strong>${escapeHtml(route.vehicle)}</strong><br>${escapeHtml(stop.locationId)}<br><small>${orders}<br>Arrival: ${escapeHtml(stop.arrival)}<br>Departure: ${escapeHtml(stop.departure)}</small>`);
            });
        });

        const droppedGroups = new Map();
        (data.dropped || []).forEach(order => {
            const key = `${order.latitude},${order.longitude}`;
            if (!droppedGroups.has(key)) droppedGroups.set(key, []);
            droppedGroups.get(key).push(order);
        });
        droppedGroups.forEach(orders => {
            const point = [orders[0].latitude, orders[0].longitude];
            allPoints.push(point);
            droppedPoints.push(point);
            const icon = L.divIcon({ className: '', html: `<span class="map-dropped">${orders.length > 1 ? orders.length : '!'}</span>`, iconSize: [26, 26], iconAnchor: [13, 13] });
            L.marker(point, { icon, zIndexOffset: 1000 })
                .addTo(map)
                .bindPopup(`<strong>${orders.length} unscheduled order(s)</strong><br>` + orders.map(order => `<strong>${escapeHtml(order.orderId)}</strong><br><small>${escapeHtml(order.reason)}</small>`).join('<hr>'));
        });

        bounds = allPoints.length ? L.latLngBounds(allPoints) : null;
        fit();
        setTimeout(() => map?.invalidateSize(), 50);
    }

    function filter(vehicle) {
        if (!map) return;
        selectedVehicle = vehicle;
        const points = [];
        routes.forEach(route => {
            const visible = !vehicle || route.vehicle === vehicle;
            if (visible) { route.layer.addTo(map); points.push(...(roadMode && route.roadPoints ? route.roadPoints : route.points)); }
            else map.removeLayer(route.layer);
        });
        // Keep all dropped markers, but focus a selected route rather than distant rejected stops.
        if (!vehicle || !points.length) points.push(...droppedPoints);
        bounds = points.length ? L.latLngBounds(points) : null;
        fit();
    }

    function setRoadRoute(vehicle, points) {
        const route = routes.find(r => r.vehicle === vehicle);
        if (route) route.roadPoints = points.map(p => [p.latitude, p.longitude]);
    }

    function setMode(useRoads) {
        roadMode = useRoads;
        routes.forEach(route => {
            if (!route.line) return;
            // Never present a straight-line fallback as road directions.
            route.line.setLatLngs(useRoads ? (route.roadPoints || []) : route.points);
        });
        filter(selectedVehicle);
    }

    function fit() {
        if (!map) return;
        if (bounds?.isValid()) map.fitBounds(bounds, { padding: [38, 38], maxZoom: 13 });
        else map.setView([39.5, -98.35], 4);
    }

    function focus(latitude, longitude) {
        if (map) map.setView([latitude, longitude], 17);
    }

    function downloadJson(fileName, content) {
        downloadText(fileName, content, 'application/json');
    }

    function downloadText(fileName, content, contentType) {
        const blob = new Blob([content], { type: contentType || 'text/plain;charset=utf-8' });
        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileName;
        anchor.click();
        URL.revokeObjectURL(url);
    }

    return { render, preview, filter, fit, focus, dispose, setRoadRoute, setMode, downloadJson, downloadText };
})();
