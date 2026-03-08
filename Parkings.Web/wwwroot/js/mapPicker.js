window.parkingMapPicker = {
    map: null,
    placemark: null,

    init(elementId, lat, lon, dotnetRef) {
        ymaps.ready(() => {
            if (this.map) { this.map.destroy(); }

            this.map = new ymaps.Map(elementId, {
                center: [lat, lon],
                zoom: 14
            });

            if (lat !== 59.94) {
                this.placemark = new ymaps.Placemark([lat, lon]);
                this.map.geoObjects.add(this.placemark);
            }

            this.map.events.add('click', async (e) => {
                const coords = e.get('coords');
                const [newLat, newLon] = coords;

                if (this.placemark) this.map.geoObjects.remove(this.placemark);
                this.placemark = new ymaps.Placemark(coords);
                this.map.geoObjects.add(this.placemark);

                // Обратное геокодирование — получаем адрес по координатам
                let address = '';
                try {
                    const result = await ymaps.geocode(coords);
                    const obj = result.geoObjects.get(0);
                    if (obj) address = obj.getAddressLine();
                } catch (e) { }

                dotnetRef.invokeMethodAsync('OnMapPicked', newLat, newLon, address);
            });
        });
    }
};