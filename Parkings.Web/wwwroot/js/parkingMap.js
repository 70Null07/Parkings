window.parkingMap = {
    map: null,
    placemarks: {},

    init(elementId, parkingsJson) {
        const tryInit = () => {
            if (typeof ymaps === 'undefined' || !ymaps.Map) {
                setTimeout(tryInit, 200);
                return;
            }

            ymaps.ready(() => {
                const parkings = JSON.parse(parkingsJson);

                this.map = new ymaps.Map(elementId, {
                    center: [59.94, 30.32],
                    zoom: 11,
                    controls: ['zoomControl', 'fullscreenControl']
                });

                this.placemarks = {};

                parkings.forEach(p => {
                    const lat = p.latitude;
                    const lon = p.longitude;
                    if (!lat || !lon) return;

                    const freeSpots = p.freeSpots ?? 0;
                    const busySpots = p.busySpots ?? 0;
                    const name = p.name ?? '';
                    const address = p.address ?? '';
                    const id = p.id;

                    const placemark = new ymaps.Placemark(
                        [lat, lon],
                        {
                            balloonContentHeader: `<b>${name}</b>`,
                            balloonContentBody: `
                                <div style="font-family:Arial;font-size:13px;padding:4px 0">
                                    <div style="color:#556B82;margin-bottom:6px">${address}</div>
                                    <div style="display:flex;gap:12px;margin-bottom:8px">
                                        <span style="color:#107E3E;font-weight:700">
                                            🟢 ${freeSpots} свободно
                                        </span>
                                        <span style="color:#BB0000;font-weight:700">
                                            🔴 ${busySpots} занято
                                        </span>
                                    </div>
                                    <a href="/parking/${id}"
                                       style="color:#0070F2;font-weight:600;text-decoration:none">
                                        → Открыть парковку
                                    </a>
                                </div>
                            `,
                        },
                        {
                            preset: freeSpots > 0
                                ? 'islands#greenDotIconWithCaption'
                                : 'islands#redDotIconWithCaption',
                            iconCaption: name,
                            iconCaptionMaxWidth: '150'
                        }
                    );

                    this.map.geoObjects.add(placemark);
                    this.placemarks[id] = placemark;
                });

                if (this.map.geoObjects.getLength() > 0) {
                    try {
                        this.map.setBounds(this.map.geoObjects.getBounds(), {
                            checkZoomRange: true,
                            zoomMargin: 60
                        });
                    } catch (e) { }
                }
            });
        };

        tryInit();
    },

    focusOn(lat, lon, parkingId) {
        if (!this.map) return;

        this.map.setCenter([lat, lon], 16, { duration: 400 });

        // Открываем балун если метка есть
        const placemark = this.placemarks[parkingId];
        if (placemark) {
            placemark.balloon.open();
        }
    }
};