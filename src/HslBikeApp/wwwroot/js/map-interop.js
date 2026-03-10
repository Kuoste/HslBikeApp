// map-interop.js — Leaflet JS interop for Blazor WASM (~80 lines)
window.MapInterop = {
    _map: null,
    _markers: {},
    _polylines: [],
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
            const radius = isSelected ? 14 : 10;
            const opts = {
                radius: radius,
                fillColor: m.color,
                color: isSelected ? '#000' : '#fff',
                weight: isSelected ? 3 : 2,
                opacity: 1,
                fillOpacity: 0.85
            };

            if (this._markers[m.id]) {
                this._markers[m.id].setLatLng([m.lat, m.lon]);
                this._markers[m.id].setStyle(opts);
                this._markers[m.id].setRadius(radius);
            } else {
                const marker = L.circleMarker([m.lat, m.lon], opts).addTo(this._map);
                marker.on('click', (e) => {
                    L.DomEvent.stopPropagation(e);
                    if (this._dotNetRef) {
                        this._dotNetRef.invokeMethodAsync('OnMarkerClicked', m.id);
                    }
                });
                this._markers[m.id] = marker;
            }

            // Tooltip with station name and bikes
            this._markers[m.id].bindTooltip(
                `<b>${m.name}</b><br>${m.bikes} bikes / ${m.spaces} spaces${m.badge ? '<br>' + m.badge : ''}`,
                { direction: 'top', offset: [0, -10] }
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

    invalidateSize: function () {
        if (this._map) this._map.invalidateSize();
    }
};
