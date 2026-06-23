window.benchmarkCharts = {

    renderGrafico: function (canvasId, dados, exibirLegenda, unidade) {

        const canvas = document.getElementById(canvasId);

        if (!canvas) {
            return;
        }

        // Obtém as cores atuais do tema Fluent UI
        const styles = getComputedStyle(document.documentElement);

        const textColor =
            styles.getPropertyValue('--colorNeutralForeground1').trim() ||
            styles.getPropertyValue('--neutral-foreground-rest').trim() ||
            '#FFFFFF';

        const gridColor =
            styles.getPropertyValue('--colorNeutralStroke2').trim() ||
            styles.getPropertyValue('--neutral-stroke-rest').trim() ||
            'rgba(255,255,255,0.12)';

        // Defaults globais do Chart.js
        Chart.defaults.color = textColor;
        Chart.defaults.borderColor = gridColor;

        const cacheKey = "_" + canvasId;

        if (window[cacheKey]) {
            window[cacheKey].destroy();
        }

        window[cacheKey] = new Chart(canvas, {
            type: "bar",

            data: {
                labels: dados.labels,
                datasets: dados.datasets
            },

            options: {

                responsive: true,

                plugins: {

                    legend: {
                        display: exibirLegenda !== false,
                        position: "bottom",

                        labels: {
                            color: textColor,
                            usePointStyle: true,
                            pointStyle: "rectRounded",
                            padding: 16
                        }
                    },

                    tooltip: {
                        callbacks: {
                            label: function (context) {

                                let label = context.dataset.label || '';

                                if (label) {
                                    label += ': ';
                                }

                                if (context.parsed.y !== null) {

                                    let sufixo = unidade;

                                    if (
                                        unidade === "tokens" &&
                                        context.label === "Tokens/s"
                                    ) {
                                        sufixo = "tokens/s";
                                    }

                                    label += context.parsed.y + " " + sufixo;
                                }

                                return label;
                            }
                        }
                    }
                },

                scales: {

                    x: {

                        ticks: {
                            color: textColor
                        },

                        grid: {
                            color: gridColor
                        },

                        border: {
                            color: gridColor
                        }
                    },

                    y: {

                        ticks: {
                            color: textColor,

                            callback: function (value) {
                                return value + " " +
                                    (unidade === "tokens"
                                        ? "t"
                                        : unidade);
                            }
                        },

                        grid: {
                            color: gridColor
                        },

                        border: {
                            color: gridColor
                        }
                    }
                }
            }
        });
    },

    downloadGrafico: function (canvasId, nomeFicheiro) {

        const canvas = document.getElementById(canvasId);

        if (!canvas) {
            return;
        }

        const imageURI = canvas.toDataURL("image/png");

        const link = document.createElement("a");
        link.download = nomeFicheiro + ".png";
        link.href = imageURI;

        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
};