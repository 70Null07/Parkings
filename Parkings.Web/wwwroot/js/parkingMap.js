window.parkingMap = {
    map: null,
    placemarks: {},
    dotnetRef: null,
    favoriteIds: [],
    filterPolygon: null,
    filterPoints: [],
    filterPolygons: {},   // { id: { polygon, points } }
    editingId: null,      // id редактируемого полигона

    init(elementId, parkingsJson, dotnetRef, favIdsJson, savedPolygonsJson) {
        this.dotnetRef = dotnetRef;
        try { this.favoriteIds = JSON.parse(favIdsJson || '[]'); } catch { this.favoriteIds = []; }

        const tryInit = () => {
            if (typeof ymaps === 'undefined' || !ymaps.Map) {
                setTimeout(tryInit, 200);
                return;
            }
            ymaps.ready(() => {
                if (this.map) { this.map.destroy(); this.map = null; }
                this.placemarks = {};
                this.filterPolygons = {};  // ← сброс
                this.editingId = null;

                const parkings = JSON.parse(parkingsJson);

                this.map = new ymaps.Map(elementId, {
                    center: [59.94, 30.32],
                    zoom: 11,
                    controls: ['zoomControl', 'fullscreenControl']
                });

                parkings.forEach(p => {
                    if (!p.latitude || !p.longitude) return;
                    this._addPlacemark(p);
                });

                if (this.map.geoObjects.getLength() > 0) {
                    try {
                        this.map.setBounds(this.map.geoObjects.getBounds(),
                            { checkZoomRange: true, zoomMargin: 60 });
                    } catch (e) { }
                }

                if (savedPolygonsJson) {
                    this.loadAllPolygons(savedPolygonsJson);
                }
            });
        };
        tryInit();
    },

    _buildBalloon(p) {
        const isFav = this.favoriteIds.includes(p.id);
        const starLabel = isFav ? '★ В избранном' : '☆ В избранное';
        const starStyle = isFav
            ? 'color:#E8A000; font-weight:700; cursor:pointer; border:none; background:none; font-size:13px;'
            : 'color:#556B82; cursor:pointer; border:none; background:none; font-size:13px;';
        const starBtn = this.dotnetRef
            ? `<button onclick="window.parkingMap._toggleFav(${p.id})" style="${starStyle}">${starLabel}</button>`
            : '';

        return `
            <div style="font-family:Arial;font-size:13px;padding:4px 0;min-width:200px">
                <div style="color:#556B82;margin-bottom:6px">${p.address}</div>
                <div style="display:flex;gap:12px;margin-bottom:8px">
                    <span style="color:#107E3E;font-weight:700">🟢 ${p.freeSpots} свободно</span>
                    <span style="color:#BB0000;font-weight:700">🔴 ${p.busySpots} занято</span>
                </div>
                <div style="display:flex;justify-content:space-between;align-items:center">
                    <a href="/parking/${p.id}"
                       style="color:#0070F2;font-weight:600;text-decoration:none">
                        → Открыть
                    </a>
                    ${starBtn}
                </div>
            </div>`;
    },

    _addPlacemark(p) {
        const isFav = this.favoriteIds.includes(p.id);

        // Выбираем preset в зависимости от избранного и загруженности
        let preset;
        if (isFav) {
            preset = 'islands#yellowDotIconWithCaption';
        } else {
            preset = p.freeSpots > 0
                ? 'islands#greenDotIconWithCaption'
                : 'islands#redDotIconWithCaption';
        }

        const placemark = new ymaps.Placemark(
            [p.latitude, p.longitude],
            {
                balloonContentHeader: `<b>${isFav ? '⭐ ' : ''}${p.name}</b>`,
                balloonContentBody: this._buildBalloon(p),
            },
            {
                preset,
                iconCaption: p.name,
                iconCaptionMaxWidth: '150'
            }
        );
        this.map.geoObjects.add(placemark);
        this.placemarks[p.id] = { placemark, data: p };
    },

    _toggleFav(parkingId) {
        if (!this.dotnetRef) return;
        this.dotnetRef.invokeMethodAsync('ToggleFavoriteFromMap', parkingId);
    },

    updateFavorites(favIdsJson) {
        try { this.favoriteIds = JSON.parse(favIdsJson); } catch { return; }

        Object.values(this.placemarks).forEach(({ placemark, data }) => {
            const isFav = this.favoriteIds.includes(data.id);

            // Обновляем иконку
            let preset;
            if (isFav) {
                preset = 'islands#yellowDotIconWithCaption';
            } else {
                preset = data.freeSpots > 0
                    ? 'islands#greenDotIconWithCaption'
                    : 'islands#redDotIconWithCaption';
            }
            placemark.options.set('preset', preset);
            placemark.properties.set('balloonContentHeader',
                `<b>${isFav ? '⭐ ' : ''}${data.name}</b>`);
            placemark.properties.set('balloonContentBody', this._buildBalloon(data));
        });
    },

    focusOn(lat, lon, parkingId) {
        if (!this.map) return;
        this.map.setCenter([lat, lon], 16, { duration: 400 });
        const entry = this.placemarks[parkingId];
        if (entry) entry.placemark.balloon.open();
    },

    _addPolygon(id, pointsJson) {
        try {
            const pts = JSON.parse(pointsJson);
            if (!pts || pts.length < 3) return;

            const polygon = new ymaps.Polygon(
                [pts], {},
                {
                    fillColor: '#0070F230',
                    strokeColor: '#0070F2',
                    strokeWidth: 2,
                    fillOpacity: 0.3
                }
            );
            this.map.geoObjects.add(polygon);
            this.filterPolygons[id] = { polygon, points: pts };
        } catch { }
    },

    enableDrawPolygon() {
        if (!this.map) return;
        this.editingId = null;

        // Временный полигон для рисования
        this._drawingPolygon = new ymaps.Polygon(
            [[]], {},
            {
                fillColor: '#0070F230',
                strokeColor: '#0070F2',
                strokeWidth: 2,
                fillOpacity: 0.3,
                editorDrawingCursor: 'crosshair',
                editorMaxPoints: 30,
            }
        );
        this.map.geoObjects.add(this._drawingPolygon);
        this._drawingPolygon.editor.startDrawing();
    },

    enableEditPolygon(id) {
        if (!this.map || !this.filterPolygons[id]) return;
        this.editingId = id;

        const { polygon } = this.filterPolygons[id];
        polygon.editor.startEditing();
    },

    finishDrawPolygon() {
        // Редактирование существующего
        if (this.editingId !== null) {
            const entry = this.filterPolygons[this.editingId];
            if (!entry) return '[]';
            entry.polygon.editor.stopEditing();
            const coords = entry.polygon.geometry.getCoordinates()[0] ?? [];
            entry.points = coords;
            this.editingId = null;
            return JSON.stringify(coords);
        }

        // Новый полигон
        if (!this._drawingPolygon) return '[]';
        this._drawingPolygon.editor.stopEditing();
        const coords = this._drawingPolygon.geometry.getCoordinates()[0] ?? [];

        // Удаляем временный, пересоздаём нормальный (без редактора)
        this.map.geoObjects.remove(this._drawingPolygon);
        this._drawingPolygon = null;

        this._pendingCoords = coords;
        return JSON.stringify(coords);
    },

    confirmNewPolygon(id, pointsJson) {
        if (!this._pendingCoords) return;
        this._addPolygon(id, JSON.stringify(this._pendingCoords));
        this._pendingCoords = null;
    },

    clearPolygon() {
        if (this.filterPolygon) {
            this.filterPolygon.editor.stopEditing();
            this.map.geoObjects.remove(this.filterPolygon);
            this.filterPolygon = null;
        }
        this.filterPoints = [];
    },

    loadSavedPolygon(pointsJson) {
        try {
            const pts = JSON.parse(pointsJson);
            if (!pts || pts.length < 3) return;

            if (this.filterPolygon) {
                this.filterPolygon.editor.stopEditing();
                this.map.geoObjects.remove(this.filterPolygon);
            }

            this.filterPolygon = new ymaps.Polygon(
                [pts],
                {},
                {
                    fillColor: '#0070F230',
                    strokeColor: '#0070F2',
                    strokeWidth: 2,
                    fillOpacity: 0.3
                }
            );
            this.map.geoObjects.add(this.filterPolygon);
            this.filterPoints = pts;
        } catch { }
    },

    cancelDraw() {
        if (this._drawingPolygon) {
            this._drawingPolygon.editor.stopEditing();
            this.map.geoObjects.remove(this._drawingPolygon);
            this._drawingPolygon = null;
        }
        if (this.editingId !== null) {
            const entry = this.filterPolygons[this.editingId];
            if (entry) entry.polygon.editor.stopEditing();
            this.editingId = null;
        }
    },

    loadAllPolygons(polygonsJson) {
        try {
            const list = JSON.parse(polygonsJson);
            list.forEach(p => this._addPolygon(p.id, p.pointsJson));
        } catch { }
    },
    removePolygon(id) {
        const entry = this.filterPolygons[id];
        if (!entry) return;
        try { entry.polygon.editor.stopEditing(); } catch { }
        this.map.geoObjects.remove(entry.polygon);
        delete this.filterPolygons[id];
    },
};