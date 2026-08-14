window.benchmarkCharts = {

    renderGrafico: function (canvasId, dados, unidade, tipo) {

        const canvas = document.getElementById(canvasId);
        if (!canvas) return false;

        // Tipo de gráfico: "bar" (padrão) ou "radar"
        tipo = tipo || "bar";
        const ehRadar = tipo === "radar";

        // 1. Obtém as cores dinâmicas do tema (Fluent UI)
        const styles = getComputedStyle(document.documentElement);

        const textColor =
            styles.getPropertyValue('--colorNeutralForeground1').trim() ||
            styles.getPropertyValue('--neutral-foreground-rest').trim() ||
            '#FFFFFF';

        const gridColor =
            styles.getPropertyValue('--colorNeutralStroke2').trim() ||
            styles.getPropertyValue('--neutral-stroke-rest').trim() ||
            'rgba(255,255,255,0.12)';

        // Aplica os padrões globais do Chart.js
        Chart.defaults.color = textColor;
        Chart.defaults.borderColor = gridColor;

        // 2. Limpa o cache do gráfico anterior para evitar sobreposição de memória
        const cacheKey = "_" + canvasId;
        if (window[cacheKey]) {
            window[cacheKey].destroy();
        }

        // 3. Criação do novo gráfico
        const legend = {
            display: ehRadar, // No radar a legenda identifica os modelos; nas barras o nome está no eixo X
            labels: {
                color: textColor,
                boxWidth: 12,
                font: { size: 12 }
            }
        };

        const tooltip = {
            callbacks: {
                label: function (context) {
                    let valor = context.parsed.y !== null && context.parsed.y !== undefined
                        ? context.parsed.y
                        : (context.parsed.r !== null && context.parsed.r !== undefined ? context.parsed.r : 0);
                    return `Valor: ${valor} ${unidade}`;
                }
            }
        };

        const escalas = ehRadar
            ? {
                r: {
                    min: 0,
                    max: 5,
                    beginAtZero: true,
                    ticks: {
                        stepSize: 1,
                        color: textColor,
                        backdropColor: 'transparent'
                    },
                    grid: { color: gridColor },
                    angleLines: { color: gridColor },
                    pointLabels: {
                        color: textColor,
                        font: { size: 11 }
                    }
                }
            }
            : {
                x: {
                    maxBarThickness: 70,
                    ticks: {
                        color: textColor,
                        maxRotation: 0,
                        minRotation: 0,
                        autoSkip: false,
                        font: {
                            size: 11
                        },
                        padding: 10
                    },
                    grid: { display: false },
                    border: { color: gridColor }
                },
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: textColor,
                        padding: 8,
                        callback: function (value) {
                            return value + " " + (unidade === "tokens" ? "t" : unidade);
                        }
                    },
                    grid: { color: gridColor },
                    border: { color: gridColor }
                }
            };

        window[cacheKey] = new Chart(canvas, {
            type: tipo,
            data: dados,
            options: {
                responsive: true,
                maintainAspectRatio: false, // Respeita rigorosamente a altura do container CSS
                plugins: {
                    legend: legend,
                    tooltip: tooltip
                },
                scales: escalas
            }
        });

        return true;
    },

    downloadGrafico: function (canvasId, nomeFicheiro) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        const imageURI = canvas.toDataURL("image/png");
        const link = document.createElement("a");
        link.download = nomeFicheiro + ".png";
        link.href = imageURI;

        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
};