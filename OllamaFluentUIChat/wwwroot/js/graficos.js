window.benchmarkCharts = {

    renderGrafico: function (canvasId, dados, unidade) {

        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

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

        // 3. Criação do novo gráfico com suporte a múltiplas linhas no eixo X
        window[cacheKey] = new Chart(canvas, {
            type: "bar",
            data: dados,
            options: {
                responsive: true,
                maintainAspectRatio: false, // Respeita rigorosamente a altura do container CSS
                plugins: {
                    legend: {
                        display: false // Oculta a legenda já que o nome do modelo está no eixo X
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                let valor = context.parsed.y !== null ? context.parsed.y : 0;
                                return `Valor: ${valor} ${unidade}`;
                            }
                        }
                    }
                },
                scales: {
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
                }
            }
        });
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