window.occupancyChart = {
    instance: null,

    render(canvasId, labels, busyData, freeData, type = 'bar') {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;
        if (this.instance) { this.instance.destroy(); this.instance = null; }

        this.instance = new Chart(ctx, {
            type: type,
            data: {
                labels,
                datasets: [
                    {
                        label: 'Занято',
                        data: busyData,
                        backgroundColor: 'rgba(187,0,0,0.7)',
                        borderColor: '#BB0000',
                        borderWidth: 2,
                        borderRadius: type === 'bar' ? 4 : 0,
                        fill: type === 'line',
                        tension: 0.3,
                    },
                    {
                        label: 'Свободно',
                        data: freeData,
                        backgroundColor: 'rgba(16,126,62,0.7)',
                        borderColor: '#107E3E',
                        borderWidth: 2,
                        borderRadius: type === 'bar' ? 4 : 0,
                        fill: type === 'line',
                        tension: 0.3,
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: { legend: { position: 'top' } },
                scales: {
                    x: { stacked: type === 'bar', ticks: { maxRotation: 45, font: { size: 11 } } },
                    y: { stacked: type === 'bar', beginAtZero: true, ticks: { stepSize: 1 } }
                }
            }
        });
    },

    destroy() {
        if (this.instance) {
            this.instance.destroy();
            this.instance = null;
        }
    }
};