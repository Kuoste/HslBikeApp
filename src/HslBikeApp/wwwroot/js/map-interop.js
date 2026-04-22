// map-interop.js — Leaflet JS interop for Blazor WASM
window.MapInterop = {
    _map: null,
    _markers: {},
    _polylines: [],
    _destinationLines: [],
    _dotNetRef: null,
    _selectedId: null,

    initMap: function (elementId, lat, lon, zoom, dotNetRef) {
        this._dotNetRef = dotNetRef;
        this._map = L.map(elementId, { zoomControl: false }).setView([lat, lon], zoom);
        this._map.options.minZoom = 10;
        this._map.options.maxZoom = 18;

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
        }).addTo(this._map);

        this._map.on('click', () => {
            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync('OnMapClicked');
            }
        });
    },

    updateMarkers: function (markersJson) {
        const markers = JSON.parse(markersJson);
        const currentIds = new Set(markers.map(m => m.id));

        // Remove markers no longer present
        for (const id of Object.keys(this._markers)) {
            if (!currentIds.has(id)) {
                this._map.removeLayer(this._markers[id]);
                delete this._markers[id];
            }
        }

        for (const m of markers) {
            const isSelected = m.id === this._selectedId;
            const size = isSelected ? 32 : 26;
            const borderWidth = isSelected ? 3 : 2;
            const borderColor = isSelected ? '#000' : '#fff';
            const fontSize = isSelected ? 13 : 11;

            const html = `<div class="bike-marker" style="width:${size}px;height:${size}px;background:${m.color};border:${borderWidth}px solid ${borderColor};font-size:${fontSize}px;">${m.bikes}</div>` +
                (m.badge ? `<div class="bike-badge" style="color:${m.badgeColor};">${m.badge}</div>` : '');

            const icon = L.divIcon({
                className: 'bike-marker-container',
                html: html,
                iconSize: [size, size + (m.badge ? 14 : 0)],
                iconAnchor: [size / 2, size / 2]
            });

            if (this._markers[m.id]) {
                this._markers[m.id].setLatLng([m.lat, m.lon]);
                this._markers[m.id].setIcon(icon);
            } else {
                const marker = L.marker([m.lat, m.lon], { icon: icon }).addTo(this._map);
                marker.on('click', (e) => {
                    L.DomEvent.stopPropagation(e);
                    if (this._dotNetRef) {
                        this._dotNetRef.invokeMethodAsync('OnMarkerClicked', m.id);
                    }
                });
                this._markers[m.id] = marker;
            }

            // Tooltip with station name and availability
            this._markers[m.id].bindTooltip(
                `<b>${m.name}</b><br>\ud83d\udeb2 ${m.bikes} / ${m.capacity} bikes${m.badge ? ' <span style="font-weight:700;">' + m.badge + '</span>' : ''}`,
                { direction: 'top', offset: [0, -size / 2 - 4] }
            );
        }
    },

    setMarkerSelected: function (stationId) {
        this._selectedId = stationId;
    },

    updatePolylines: function (polylinesJson) {
        this.clearPolylines();
        const lines = JSON.parse(polylinesJson);
        for (const line of lines) {
            const polyline = L.polyline(line.coords, {
                color: '#009688',
                opacity: 0.6,
                weight: 3
            }).addTo(this._map);
            this._polylines.push(polyline);
        }
    },

    clearPolylines: function () {
        for (const p of this._polylines) {
            this._map.removeLayer(p);
        }
        this._polylines = [];
    },

    updateDestinationRoutes: function (routesJson) {
        this.clearDestinationRoutes();
        const routes = JSON.parse(routesJson);
        for (const route of routes) {
            const line = L.polyline([route.from, route.to], {
                color: 'rgba(33, 150, 243, 0.6)',
                weight: route.weight || 3,
                dashArray: null,
                className: 'destination-route'
            }).addTo(this._map);
            if (route.tooltip) {
                line.bindTooltip(route.tooltip, { sticky: true });
            }
            this._destinationLines.push(line);
        }
    },

    clearDestinationRoutes: function () {
        for (const line of this._destinationLines) {
            this._map.removeLayer(line);
        }
        this._destinationLines = [];
    },

    invalidateSize: function () {
        if (this._map) this._map.invalidateSize();
    }
};

// Visibility interop — notifies Blazor when the tab becomes visible or hidden.
window.VisibilityInterop = {
    _dotNetRef: null,

    register: function (dotNetRef) {
        this._dotNetRef = dotNetRef;
        document.addEventListener('visibilitychange', () => {
            if (this._dotNetRef) {
                this._dotNetRef.invokeMethodAsync('OnVisibilityChanged', !document.hidden);
            }
        });
    },

    dispose: function () {
        this._dotNetRef = null;
    }
};
