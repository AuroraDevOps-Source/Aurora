window.routeMap = (() => {
    let map;
    let resizeObserver;
    let bounds;
    let orderMarkers = [];
    let routes = [];
    let droppedPoints = [];
    let droppedMarkers = [];
    let showDropped = true;
    let showRoutes = true;
    let roadMode = false;
    let selectedVehicle = null;

    const splitterElements = new WeakSet();
    function initSplitter(elementId) {
        const grid = document.getElementById(elementId);
        const divider = grid?.querySelector('.planner-divider');
        if (!divider || splitterElements.has(grid)) return;
        splitterElements.add(grid);
        const key = 'aurora-manifest-map-split';
        let fraction = .62;
        try { const saved = Number(sessionStorage.getItem(key)); if (saved >= .25 && saved <= .75) fraction = saved; } catch { }
        function apply(value, save = true) {
            fraction = Math.max(.25, Math.min(.75, value));
            grid.style.setProperty('--planner-split', String(fraction));
            grid.style.setProperty('--planner-left', `${fraction / (1 - fraction)}fr`);
            const percent = Math.round(fraction * 100);
            divider.setAttribute('aria-valuenow', String(percent));
            divider.setAttribute('aria-valuetext', `Manifest ${percent}%, map ${100 - percent}%`);
            if (save) { try { sessionStorage.setItem(key, String(fraction)); } catch { } }
        }
        let pointer = null;
        divider.addEventListener('pointerdown', event => {
            if (event.button !== 0) return;
            pointer = event.pointerId;
            divider.setPointerCapture(pointer);
            event.preventDefault();
        });
        divider.addEventListener('pointermove', event => {
            if (pointer !== event.pointerId) return;
            const rect = grid.getBoundingClientRect();
            if (rect.width > 14) apply((event.clientX - rect.left - 7) / (rect.width - 14));
        });
        const end = () => { pointer = null; };
        divider.addEventListener('pointerup', end);
        divider.addEventListener('pointercancel', end);
        divider.addEventListener('lostpointercapture', end);
        divider.addEventListener('dblclick', () => apply(.62));
        divider.addEventListener('keydown', event => {
            const value = { ArrowLeft: fraction - .02, ArrowRight: fraction + .02, Home: .25, End: .75 }[event.key];
            if (value === undefined) return;
            event.preventDefault();
            apply(value);
        });
        apply(fraction, false);
    }

    function dispose() {
        resizeObserver?.disconnect();
        resizeObserver = null;
        if (map) map.remove();
        map = null;
        bounds = null;
        orderMarkers = [];
        routes = [];
        droppedPoints = [];
        droppedMarkers = [];
        showDropped = true;
        showRoutes = true;
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
        resizeObserver = new ResizeObserver(() => map?.invalidateSize({ pan: false }));
        resizeObserver.observe(element);
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
            const marker = L.marker(point, { icon }).addTo(map).bindPopup(
                `<strong>${first.isDepot ? 'Depot' : 'Unassigned delivery location'}</strong><br>` +
                group.slice(0, 10).map(p => `${escapeHtml(p.id)} · ${escapeHtml(p.label)}`).join('<br>') +
                (group.length > 10 ? `<br>and ${group.length - 10} more` : ''));
            group.forEach(p => orderMarkers.push({ id: String(p.id), marker }));
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
                            : `<span class="map-stop" style="--route:${route.color}">${escapeHtml(String(stop.sequence ?? index))}</span>`,
                        iconSize: [28, 28], iconAnchor: [14, 14]
                    })
                }).addTo(layer);
                (stop.orderIds || []).forEach(id => orderMarkers.push({ id: String(id), vehicle: route.vehicle, marker }));
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
            const label = orders.map(order => order.number).join(", ");
            const width = Math.max(26, label.length * 7 + 12);
            const icon = L.divIcon({ className: '', html: `<span class="map-dropped" style="width:${width}px">${escapeHtml(label)}</span>`, iconSize: [width, 26], iconAnchor: [width / 2, 13] });
            const marker = L.marker(point, { icon, zIndexOffset: 1000 })
                .addTo(map)
                .bindPopup(`<strong>${orders.length} unscheduled order(s)</strong><br>` + orders.map(order => `<strong>${escapeHtml(String(order.number))}. ${escapeHtml(order.orderId)}</strong><br><small>${escapeHtml(order.reason)}</small>`).join('<hr>'));
            droppedMarkers.push(marker);
            orders.forEach(order => orderMarkers.push({ id: String(order.orderId), marker }));
        });

        bounds = allPoints.length ? L.latLngBounds(allPoints) : null;
        fit();
        setTimeout(() => map?.invalidateSize(), 50);
    }

    function filter(vehicle, includeDropped = true, includeRoutes = true) {
        if (!map) return;
        selectedVehicle = vehicle;
        showDropped = includeDropped;
        showRoutes = includeRoutes;
        const points = [];
        routes.forEach(route => {
            const visible = showRoutes && (!vehicle || route.vehicle === vehicle);
            if (visible) { route.layer.addTo(map); points.push(...(roadMode && route.roadPoints ? route.roadPoints : route.points)); }
            else map.removeLayer(route.layer);
        });
        droppedMarkers.forEach(marker => {
            if (showDropped) marker.addTo(map);
            else map.removeLayer(marker);
        });
        // Fit everything the planner chose to show, including distant unrouted orders.
        if (showDropped) points.push(...droppedPoints);
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
        filter(selectedVehicle, showDropped, showRoutes);
    }

    function fit() {
        if (!map) return;
        if (bounds?.isValid()) map.fitBounds(bounds, { padding: [38, 38], maxZoom: 13 });
        else map.setView([39.5, -98.35], 4);
    }

    function focus(latitude, longitude) {
        if (map) map.setView([latitude, longitude], 17);
    }

    function focusOrder(orderId, vehicle) {
        const match = orderMarkers.find(entry => entry.id === String(orderId) && (!vehicle || entry.vehicle === vehicle));
        if (!map || !match) return;
        map.setView(match.marker.getLatLng(), 16);
        match.marker.openPopup();
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

    function initDeliveryDrag(elementId, receiver) {
        const grid = document.getElementById(elementId);
        if (!grid || grid.deliveryDragInitialized) return;
        grid.deliveryDragInitialized = true;
        let active = null, target = null;
        const clear = () => {
            target?.classList.remove('delivery-drop-target');
            active = target = null;
        };
        grid.addEventListener('pointerdown', event => {
            const handle = event.target.closest('[data-drag-order]');
            if (!handle || handle.disabled || event.button !== 0) return;
            event.preventDefault();
            handle.setPointerCapture(event.pointerId);
            active = { handle, pointer: event.pointerId, x: event.clientX, y: event.clientY, moved: false };
        });
        grid.addEventListener('pointermove', event => {
            if (!active || event.pointerId !== active.pointer) return;
            if (Math.hypot(event.clientX - active.x, event.clientY - active.y) < 5 && !active.moved) return;
            active.moved = true;
            const next = document.elementFromPoint(event.clientX, event.clientY)?.closest('[data-route-truck]');
            target?.classList.remove('delivery-drop-target');
            target = next && grid.contains(next) && next.dataset.routeTruck !== active.handle.dataset.fromTruck ? next : null;
            target?.classList.add('delivery-drop-target');
        });
        grid.addEventListener('pointerup', event => {
            if (!active || event.pointerId !== active.pointer) return;
            const order = active.handle.dataset.dragOrder;
            const truck = active.moved && !active.handle.disabled ? target?.dataset.routeTruck : null;
            clear();
            if (truck) receiver.invokeMethodAsync('MoveDelivery', order, truck).catch(error => console.error('Delivery move failed', error));
        });
        grid.addEventListener('pointercancel', clear);
        grid.addEventListener('lostpointercapture', clear);
        grid.addEventListener('keydown', event => { if (event.key === 'Escape') clear(); });
    }

    return { initDeliveryDrag, initSplitter, render, preview, filter, fit, focus, focusOrder, dispose, setRoadRoute, setMode, downloadJson, downloadText };
})();
