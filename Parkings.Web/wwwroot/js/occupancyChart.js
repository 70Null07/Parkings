window.occupancyChart = {
    instance: null,

    render(canvasId, labels, busyData, freeData) {
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;

        if (this.instance) {
            this.instance.destroy();
            this.instance = null;
        }

        this.instance = new Chart(ctx, {
            type: 'bar',
            data: {
                labels: labels,
                datasets: [
                    {
                        label: 'Занято',
                        data: busyData,
                        backgroundColor: 'rgba(187, 0, 0, 0.7)',
                        borderColor: '#BB0000',
                        borderWidth: 1,
                        borderRadius: 4,
                    },
                    {
                        label: 'Свободно',
                        data: freeData,
                        backgroundColor: 'rgba(16, 126, 62, 0.7)',
                        borderColor: '#107E3E',
                        borderWidth: 1,
                        borderRadius: 4,
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { position: 'top' },
                    tooltip: {
                        callbacks: {
                            label: ctx => {
                                const total = ctx.chart.data.datasets
                                    .reduce((s, d) => s + (d.data[ctx.dataIndex] || 0), 0);
                                const pct = total > 0
                                    ? Math.round(ctx.raw / total * 100) : 0;
                                return `${ctx.dataset.label}: ${ctx.raw} (${pct}%)`;
                            }
                        }
                    }
                },
                scales: {
                    x: {
                        stacked: true,
                        ticks: { maxRotation: 45, font: { size: 11 } }
                    },
                    y: {
                        stacked: true,
                        beginAtZero: true,
                        ticks: { stepSize: 1 }
                    }
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