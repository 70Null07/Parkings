window.parkingMap = {
    init(elementId, parkingsJson) {
        const tryInit = () => {
            if (typeof ymaps === 'undefined' || !ymaps.Map) {
                setTimeout(tryInit, 200);
                return;
            }

            ymaps.ready(() => {
                const parkings = JSON.parse(parkingsJson);
                console.log('Parkings for map:', parkings); // для отладки

                const map = new ymaps.Map(elementId, {
                    center: [59.94, 30.32],
                    zoom: 11,
                    controls: ['zoomControl', 'fullscreenControl']
                });

                parkings.forEach(p => {
                    // C# JsonSerializer даёт PascalCase — читаем оба варианта
                    const lat = p.Latitude ?? p.latitude;
                    const lon = p.Longitude ?? p.longitude;
                    const id = p.Id ?? p.id;

                    if (!lat || !lon) return;

                    const freeSpots = p.FreeSpots ?? p.freeSpots ?? 0;
                    const busySpots = p.BusySpots ?? p.busySpots ?? 0;
                    const name = p.Name ?? p.name ?? '';
                    const address = p.Address ?? p.address ?? '';

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

                    map.geoObjects.add(placemark);
                });

                // Подгоняем вид если есть метки
                if (map.geoObjects.getLength() > 0) {
                    map.setBounds(map.geoObjects.getBounds(), {
                        checkZoomRange: true,
                        zoomMargin: 60
                    });
                }
            });
        };

        tryInit();
    }
};