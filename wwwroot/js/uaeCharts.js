window.uaeCharts = window.uaeCharts || {};
window.uaeCharts._charts = window.uaeCharts._charts || {};

function _destroy(id) {
    const ch = window.uaeCharts._charts[id];
    if (ch) { ch.destroy(); delete window.uaeCharts._charts[id]; }
}

window.uaeCharts.renderBalanceTrend = function (canvasId, labels, running, zeroLine) {
    const el = document.getElementById(canvasId);
    if (!el) return;
    _destroy(canvasId);

    const ctx = el.getContext("2d");
    const chart = new Chart(ctx, {
        type: "line",
        data: {
            labels: labels,
            datasets: [
                {
                    label: "Running Balance",
                    data: running,
                    tension: 0.35,
                    fill: true,
                    pointRadius: 2,
                    borderWidth: 2
                },
                {
                    label: "Zero Line",
                    data: zeroLine,
                    borderDash: [6, 6],
                    pointRadius: 0,
                    borderWidth: 2
                }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: "top" } },
            scales: {
                x: { grid: { display: false } },
                y: { grid: { color: "rgba(255,255,255,0.08)" } }
            }
        }
    });

    window.uaeCharts._charts[canvasId] = chart;
};

window.uaeCharts.renderInOutBars = function (canvasId, labels, totalIn, totalOut) {
    const el = document.getElementById(canvasId);
    if (!el) return;
    _destroy(canvasId);

    const ctx = el.getContext("2d");
    const chart = new Chart(ctx, {
        type: "bar",
        data: {
            labels: labels,
            datasets: [
                { label: "IN (A+E)", data: totalIn, borderWidth: 1, borderRadius: 8 },
                { label: "OUT (A+E)", data: totalOut, borderWidth: 1, borderRadius: 8 }
            ]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: { legend: { position: "top" } },
            scales: {
                x: { grid: { display: false } },
                y: { grid: { color: "rgba(255,255,255,0.08)" } }
            }
        }
    });

    window.uaeCharts._charts[canvasId] = chart;
};

window.uaeCharts.renderExpenseDonut = function (canvasId, labels, values) {
    const el = document.getElementById(canvasId);
    if (!el) return;
    _destroy(canvasId);

    const ctx = el.getContext("2d");
    const chart = new Chart(ctx, {
        type: "doughnut",
        data: {
            labels: labels,
            datasets: [{ data: values, borderWidth: 0 }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            cutout: "65%",
            plugins: {
                legend: { position: "right" }
            }
        }
    });

    window.uaeCharts._charts[canvasId] = chart;
};

window.uaeCharts.renderRiskDivergingBars = function (canvasId, labels, values) {
    const el = document.getElementById(canvasId);
    if (!el) return;
    _destroy(canvasId);

    const ctx = el.getContext("2d");
    const bg = values.map(v => v >= 0 ? "rgba(47,123,255,0.80)" : "rgba(255,70,70,0.80)");

    const chart = new Chart(ctx, {
        type: "bar",
        data: {
            labels: labels,
            datasets: [{
                label: "Risk Amount",
                data: values,
                backgroundColor: bg,
                borderWidth: 0,
                borderRadius: 8
            }]
        },
        options: {
            indexAxis: "y",
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: { display: false },
                tooltip: {
                    callbacks: {
                        label: (c) => {
                            const v = c.raw || 0;
                            const t = v >= 0 ? "Receivable" : "Payable";
                            return `${t}: ${Math.abs(v).toFixed(2)}`;
                        }
                    }
                }
            },
            scales: {
                x: { grid: { color: "rgba(255,255,255,0.08)" } },
                y: { grid: { display: false } }
            }
        }
    });

    window.uaeCharts._charts[canvasId] = chart;
};
