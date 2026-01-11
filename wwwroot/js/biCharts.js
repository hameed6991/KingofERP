// wwwroot/js/biCharts.js
(function () {
    const _charts = {};

    function _getCtx(canvasId) {
        const el = document.getElementById(canvasId);
        if (!el) return null;
        return el.getContext("2d");
    }

    function destroy(canvasId) {
        const c = _charts[canvasId];
        if (c) {
            c.destroy();
            delete _charts[canvasId];
        }
    }

    // ✅ Multi-dataset LINE (supports negative values)
    function renderLineMulti(canvasId, labels, datasets, options) {
        const ctx = _getCtx(canvasId);
        if (!ctx) return;

        // Find min/max for negative profit support
        let minY = 0, maxY = 0;
        datasets.forEach(ds => {
            (ds.data || []).forEach(v => {
                if (typeof v !== "number" || Number.isNaN(v)) return;
                minY = Math.min(minY, v);
                maxY = Math.max(maxY, v);
            });
        });

        const beginAtZero = minY >= 0;

        const cfg = {
            type: "line",
            data: { labels, datasets },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: { mode: "index", intersect: false },
                plugins: {
                    legend: { display: true },
                    tooltip: {
                        callbacks: {
                            label: function (c) {
                                const p = (options && options.currencyPrefix) ? options.currencyPrefix : "";
                                const v = c.parsed.y ?? 0;
                                return `${c.dataset.label}: ${p}${v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
                            }
                        }
                    }
                },
                scales: {
                    x: { grid: { display: false }, ticks: { maxTicksLimit: 10 } },
                    y: {
                        beginAtZero: beginAtZero,
                        suggestedMin: beginAtZero ? 0 : (minY * 1.1),
                        suggestedMax: (maxY * 1.1),
                        ticks: {
                            callback: function (value) {
                                const p = (options && options.currencyPrefix) ? options.currencyPrefix : "";
                                return p + value;
                            }
                        }
                    }
                }
            }
        };

        destroy(canvasId);
        _charts[canvasId] = new Chart(ctx, cfg);
    }

    // ✅ BAR chart
    function renderBar(canvasId, labels, values, labelName, options) {
        const ctx = _getCtx(canvasId);
        if (!ctx) return;

        const nums = (values || []).filter(v => typeof v === "number" && !Number.isNaN(v));
        const minY = Math.min(0, ...(nums.length ? nums : [0]));
        const maxY = Math.max(0, ...(nums.length ? nums : [0]));
        const beginAtZero = minY >= 0;

        const cfg = {
            type: "bar",
            data: {
                labels,
                datasets: [{
                    label: labelName || "Value",
                    data: values
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: true },
                    tooltip: {
                        callbacks: {
                            label: function (c) {
                                const p = (options && options.currencyPrefix) ? options.currencyPrefix : "";
                                const v = c.parsed.y ?? 0;
                                return `${c.dataset.label}: ${p}${v.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
                            }
                        }
                    }
                },
                scales: {
                    x: { grid: { display: false } },
                    y: {
                        beginAtZero: beginAtZero,
                        suggestedMin: beginAtZero ? 0 : (minY * 1.1),
                        suggestedMax: (maxY * 1.1),
                        ticks: {
                            callback: function (value) {
                                const p = (options && options.currencyPrefix) ? options.currencyPrefix : "";
                                return p + value;
                            }
                        }
                    }
                }
            }
        };

        destroy(canvasId);
        _charts[canvasId] = new Chart(ctx, cfg);
    }

    // ✅ Backward compatible single-line (your SalesTrend uses this)
    function renderLine(canvasId, labels, values, labelName) {
        return renderLineMulti(
            canvasId,
            labels,
            [{
                label: labelName || "Sales",
                data: values,
                tension: 0.35,
                pointRadius: 2,
                pointHoverRadius: 5,
                fill: true
            }],
            { currencyPrefix: "" }
        );
    }

    window.biCharts = { renderLine, renderLineMulti, renderBar, destroy };
})();
