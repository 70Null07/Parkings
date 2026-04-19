async function initMap() {

    await ymaps3.ready;

    const { YMap, YMapDefaultSchemeLayer, YMapDefaultFeaturesLayer, YMapMarker } = ymaps3;

    const map = new YMap(
        document.getElementById('map'),
        {
            location: {
                center: [30.31413, 59.93863],
                zoom: 12
            }
        }
    );

    map.addChild(new YMapDefaultSchemeLayer());
    map.addChild(new YMapDefaultFeaturesLayer());

    const marker = new YMapMarker({
        coordinates: [30.31413, 59.93863]
    });

    marker.element.innerHTML = "<div style='background:red;width:20px;height:20px;border-radius:50%'></div>";

    map.addChild(marker);
}

initMap();