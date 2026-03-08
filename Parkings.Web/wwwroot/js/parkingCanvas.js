window.parkingCanvas = (() => {
    let canvas, ctx, img;
    let isDrawing = false;
    let currentPoints = [];
    let spaces = [];
    let nextId = 1;
    let hoveredIdx = -1;
    let dotnetRef = null;

    let refreshInterval = null;
    let imgEl = null;
    let REFRESH_MS = 5000;  // ← const → let

    let occupancyMode = false;
    let occupancyData = [];

    function refreshImage() {
        if (!imgEl || isDrawing) return;
        // Сохраняем url= параметр, меняем только _t=
        const urlObj = new URL(imgEl.src, window.location.origin);
        urlObj.searchParams.set('_t', Date.now());
        imgEl.src = urlObj.toString();
    }

    function startRefresh() {
        if (refreshInterval) return;
        refreshInterval = setInterval(refreshImage, REFRESH_MS);
    }

    function stopRefresh() {
        clearInterval(refreshInterval);
        refreshInterval = null;
    }

    const COLORS = [
        '#00e5ff', '#76ff03', '#ff9100', '#d500f9',
        '#ff1744', '#00e676', '#ffea00', '#2979ff'
    ];

    function getColor(index) {
        return COLORS[index % COLORS.length];
    }

    function redraw() {
        if (!canvas || !ctx) return;
        ctx.clearRect(0, 0, canvas.width, canvas.height);

        if (isDrawing && img) {
            ctx.drawImage(img, 0, 0, canvas.width, canvas.height);
        }

        if (occupancyMode) {
            // Режим подсветки занятости
            spaces.forEach((space) => {
                const occ = occupancyData.find(o => o.spaceNumber === space.id);
                const isOccupied = occ ? occ.isOccupied : false;
                drawOccupancyPolygon(space.points, isOccupied, space.id);
            });
        } else {
            spaces.forEach((space, idx) => {
                drawPolygon(space.points, getColor(idx), idx === hoveredIdx, space.id);
            });
            if (currentPoints.length > 0) drawInProgress();
        }
    }

    function drawOccupancyPolygon(points, isOccupied, id) {
        if (points.length < 3) return;
        ctx.save();

        ctx.beginPath();
        ctx.moveTo(points[0].x, points[0].y);
        for (let i = 1; i < points.length; i++) ctx.lineTo(points[i].x, points[i].y);
        ctx.closePath();

        ctx.fillStyle = isOccupied ? 'rgba(255,80,80,0.45)' : 'rgba(80,200,80,0.45)';
        ctx.strokeStyle = isOccupied ? '#cc2222' : '#22aa22';
        ctx.lineWidth = 2;
        ctx.fill();
        ctx.stroke();

        const cx = points.reduce((s, p) => s + p.x, 0) / points.length;
        const cy = points.reduce((s, p) => s + p.y, 0) / points.length;
        ctx.font = 'bold 13px Arial';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = '#ffffff';
        ctx.shadowColor = 'rgba(0,0,0,0.9)';
        ctx.shadowBlur = 4;
        ctx.fillText('P' + id, cx, cy);

        ctx.restore();
    }

    function drawPolygon(points, color, isHovered, id) {
        if (points.length < 2) return;
        ctx.save();

        ctx.beginPath();
        ctx.moveTo(points[0].x, points[0].y);
        for (let i = 1; i < points.length; i++) ctx.lineTo(points[i].x, points[i].y);
        ctx.closePath();
        ctx.fillStyle = isHovered ? hexToRgba(color, 0.45) : hexToRgba(color, 0.22);
        ctx.fill();

        ctx.strokeStyle = color;
        ctx.lineWidth = isHovered ? 2.5 : 1.8;
        ctx.setLineDash([]);
        ctx.stroke();

        points.forEach(p => {
            ctx.beginPath();
            ctx.arc(p.x, p.y, 4, 0, Math.PI * 2);
            ctx.fillStyle = color;
            ctx.fill();
        });

        const cx = points.reduce((s, p) => s + p.x, 0) / points.length;
        const cy = points.reduce((s, p) => s + p.y, 0) / points.length;
        ctx.font = 'bold 13px "Courier New", monospace';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = '#ffffff';
        ctx.shadowColor = 'rgba(0,0,0,0.9)';
        ctx.shadowBlur = 5;
        ctx.fillText('P' + id, cx, cy);

        ctx.restore();
    }

    function drawInProgress() {
        ctx.save();
        ctx.strokeStyle = '#ffffff';
        ctx.lineWidth = 1.5;
        ctx.setLineDash([6, 4]);

        if (currentPoints.length > 1) {
            ctx.beginPath();
            ctx.moveTo(currentPoints[0].x, currentPoints[0].y);
            for (let i = 1; i < currentPoints.length; i++) ctx.lineTo(currentPoints[i].x, currentPoints[i].y);
            ctx.stroke();
        }

        currentPoints.forEach((p, i) => {
            ctx.beginPath();
            ctx.arc(p.x, p.y, 5, 0, Math.PI * 2);
            ctx.fillStyle = i === 0 ? '#00e5ff' : '#ffffff';
            ctx.fill();
            ctx.strokeStyle = '#000';
            ctx.lineWidth = 1;
            ctx.setLineDash([]);
            ctx.stroke();
        });

        const remaining = 4 - currentPoints.length;
        ctx.font = '12px "Courier New", monospace';
        ctx.textAlign = 'left';
        ctx.textBaseline = 'top';
        ctx.fillStyle = 'rgba(0,0,0,0.72)';
        ctx.fillRect(8, 8, 180, 24);
        ctx.fillStyle = '#00e5ff';
        ctx.fillText('Осталось точек: ' + remaining, 12, 13);

        ctx.restore();
    }

    function hexToRgba(hex, alpha) {
        const r = parseInt(hex.slice(1, 3), 16);
        const g = parseInt(hex.slice(3, 5), 16);
        const b = parseInt(hex.slice(5, 7), 16);
        return 'rgba(' + r + ',' + g + ',' + b + ',' + alpha + ')';
    }

    function getCanvasPos(e) {
        const rect = canvas.getBoundingClientRect();
        const scaleX = canvas.width / rect.width;
        const scaleY = canvas.height / rect.height;
        return { x: (e.clientX - rect.left) * scaleX, y: (e.clientY - rect.top) * scaleY };
    }

    function onClick(e) {
        if (!isDrawing) return;
        const pos = getCanvasPos(e);
        currentPoints.push(pos);

        if (currentPoints.length === 4) {
            spaces.push({ id: nextId++, points: [...currentPoints] });
            currentPoints = [];
            if (dotnetRef) dotnetRef.invokeMethodAsync('OnSpaceAdded', spaces.length);
        }
        redraw();
    }

    function onMouseMove(e) {
        if (!isDrawing) return;
        const pos = getCanvasPos(e);
        let found = -1;
        spaces.forEach((space, idx) => { if (isPointInPolygon(pos, space.points)) found = idx; });
        if (found !== hoveredIdx) { hoveredIdx = found; redraw(); }
    }

    function onRightClick(e) {
        if (!isDrawing) return;
        e.preventDefault();
        if (currentPoints.length > 0) currentPoints.pop();
        else if (spaces.length > 0) { spaces.pop(); nextId = spaces.length > 0 ? spaces[spaces.length - 1].id + 1 : 1; }
        redraw();
    }

    function isPointInPolygon(point, polygon) {
        let inside = false;
        for (let i = 0, j = polygon.length - 1; i < polygon.length; j = i++) {
            const xi = polygon[i].x, yi = polygon[i].y, xj = polygon[j].x, yj = polygon[j].y;
            if (((yi > point.y) !== (yj > point.y)) && (point.x < (xj - xi) * (point.y - yi) / (yj - yi) + xi)) inside = !inside;
        }
        return inside;
    }

    return {
        init(canvasId, ref) {
            canvas = document.getElementById(canvasId);
            ctx = canvas.getContext('2d');
            dotnetRef = ref;
            canvas.addEventListener('click', onClick);
            canvas.addEventListener('mousemove', onMouseMove);
            canvas.addEventListener('contextmenu', onRightClick);
        },

        // НОВОЕ: запускается при монтировании компонента
        startLive(imgId, intervalMs = 5000) {
            imgEl = document.getElementById(imgId);
            REFRESH_MS = intervalMs;  // сделать let вместо const
            startRefresh();
        },

        enableDraw(imgId, streamType = 'JPG', streamUrl = '') {
            stopRefresh();
            imgEl = document.getElementById(imgId);
            img = new Image();
            img.crossOrigin = 'anonymous';
            img.onload = () => { isDrawing = true; canvas.style.cursor = 'crosshair'; redraw(); };
            img.onerror = () => { isDrawing = true; canvas.style.cursor = 'crosshair'; redraw(); };

            if (streamType === 'MJPEG' && streamUrl) {
                img.src = '/api/camera/snapshot?type=MJPEG&url='
                    + encodeURIComponent(streamUrl) + '&_t=' + Date.now();
            } else {
                const urlObj = new URL(imgEl.src, window.location.origin);
                urlObj.searchParams.set('_t', Date.now());
                img.src = urlObj.toString();
            }
        },

        disableDraw() {
            isDrawing = false;
            currentPoints = [];
            canvas.style.cursor = 'default';
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            startRefresh();                     // возобновляем live-обновление
        },

        undoLast() {
            if (currentPoints.length > 0) currentPoints.pop();
            else if (spaces.length > 0) { spaces.pop(); nextId = spaces.length > 0 ? spaces[spaces.length - 1].id + 1 : 1; }
            redraw();
        },
        clearAll() { spaces = []; currentPoints = []; nextId = 1; redraw(); },
        getSpaces() { return JSON.stringify(spaces); },
        getSpaceCount() { return spaces.length; },
        dispose() { stopRefresh(); },
        loadSpaces(json) {
            try {
                const data = JSON.parse(json);
                spaces = data.map(s => ({
                    id: s.spaceNumber,
                    points: s.points.map(p => ({ x: p.x, y: p.y }))
                }));
                nextId = spaces.length > 0
                    ? Math.max(...spaces.map(s => s.id)) + 1
                    : 1;
                redraw();
            } catch (e) {
                console.error('loadSpaces error', e);
            }
        },
        setOccupancy(spacesJson) {
            try {
                // spacesJson: [{id, spaceNumber, isOccupied, points}]
                const data = JSON.parse(spacesJson);
                // Синхронизируем spaces с данными из БД если ещё не загружены
                if (spaces.length === 0) {
                    spaces = data.map(s => ({
                        id: s.spaceNumber,
                        points: s.points.map(p => ({ x: p.x, y: p.y }))
                    }));
                    nextId = spaces.length > 0
                        ? Math.max(...spaces.map(s => s.id)) + 1 : 1;
                }
                occupancyData = data.map(s => ({
                    spaceNumber: s.spaceNumber,
                    isOccupied: s.isOccupied
                }));
                redraw();
            } catch (e) { console.error('setOccupancy error', e); }
        },

        enableOccupancyMode() {
            occupancyMode = true;
            redraw();
        },

        disableOccupancyMode() {
            occupancyMode = false;
            redraw();
        },
    };
})();
