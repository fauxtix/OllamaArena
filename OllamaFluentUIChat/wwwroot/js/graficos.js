window.benchmarkCharts = {
    renderGrafico: function (canvasId, dados, exibirLegenda, unidade) {
        // ... (Mantenha o código da função renderGrafico idêntico ao passo anterior)
        const ctx = document.getElementById(canvasId);
        if (!ctx) return;
        const cacheKey = "_" + canvasId;
        if (window[cacheKey]) { window[cacheKey].destroy(); }
        window[cacheKey] = new Chart(ctx, {
            type: "bar",
            data: { labels: dados.labels, datasets: dados.datasets },
            options: {
                responsive: true,
                plugins: {
                    legend: { display: exibirLegenda !== false, position: "bottom" },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                let label = context.dataset.label || '';
                                if (label) { label += ': '; }
                                if (context.parsed.y !== null) {
                                    let sufixo = unidade;
                                    if (unidade === "tokens" && context.label === "Tokens/s") { sufixo = "tokens/s"; }
                                    label += context.parsed.y + " " + sufixo;
                                }
                                return label;
                            }
                        }
                    }
                },
                scales: { y: { ticks: { callback: function (value) { return value + " " + (unidade === "tokens" ? "t" : unidade); } } } }
            }
        });
    },

    // NOVA FUNÇÃO: Transforma o canvas em imagem e faz o download
    downloadGrafico: function (canvasId, nomeFicheiro) {
        const canvas = document.getElementById(canvasId);
        if (!canvas) return;

        // Converte o canvas para Base64 PNG
        const imageURI = canvas.toDataURL("image/png");

        // Cria um elemento <a> temporário no DOM para disparar o download
        const link = document.createElement("a");
        link.download = nomeFicheiro + ".png";
        link.href = imageURI;

        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    }
};
