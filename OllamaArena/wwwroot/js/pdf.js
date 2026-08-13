window.gerarRelatorioPDF = function (dados) {

    if (!window.jsPDF) {
        throw new Error("jsPDF não carregado.");
    }

    const doc = new window.jsPDF({ orientation: 'landscape', unit: 'pt', format: 'a4' });
    const pageHeight = doc.internal.pageSize.getHeight();
    const margin = 30;
    const contentWidth = doc.internal.pageSize.getWidth() - (margin * 2);
    let y = 0;

    const GRAY = '#4A4A4A';
    const BLUE = '#0078D4';

    // Helper para garantir arrays de strings
    const safeLines = (value) => {
        if (!value) return [""];
        if (Array.isArray(value)) return value.map(v => String(v));
        return [String(value)];
    };

    doc.setFontSize(18);
    doc.setTextColor(BLUE);
    doc.setFont('helvetica', 'bold');
    doc.text(String(dados.titulo), margin, y + 30);
    y += 52;

    doc.setFont('helvetica', 'normal');
    doc.setFontSize(9);
    doc.setTextColor(GRAY);
    doc.text(`${dados.geradoEm}: ${dados.data}`, margin, y);
    y += 26;

    const novaPaginaSeNecessario = (alturaNecessaria) => {
        if (y + alturaNecessaria > pageHeight - margin) {
            doc.addPage();
            y = margin + 20;
        }
    };

    dados.grupos.forEach((grupo) => {

        doc.setFont('helvetica', 'bold');
        doc.setFontSize(12);
        doc.setTextColor(0, 0, 0);
        novaPaginaSeNecessario(30);
        doc.text(`${dados.rotuloPrompt} #${grupo.id}`, margin, y);
        y += 16;

        doc.setFont('helvetica', 'normal');
        doc.setFontSize(9);

        const promptLines = safeLines(doc.splitTextToSize(grupo.prompt || '', contentWidth));
        novaPaginaSeNecessario(promptLines.length * 11 + 20);
        doc.text(promptLines, margin, y);
        y += promptLines.length * 11 + 6;

        doc.setFontSize(8);
        doc.setTextColor(GRAY);
        doc.text(`${dados.rotuloData}: ${grupo.data}`, margin, y);
        y += 18;

        grupo.respostas.forEach((resposta) => {

            doc.setFont('helvetica', 'bold');
            doc.setFontSize(11);
            doc.setTextColor(0, 0, 0);
            novaPaginaSeNecessario(30);
            doc.text(`${dados.rotuloModelo}: ${resposta.modelo}`, margin, y);
            y += 16;

            doc.setFont('helvetica', 'normal');
            doc.setFontSize(9);

            const perf = dados.rotulosDesempenho
                .map((r, i) => `${r}: ${resposta.desempenho[i]}`)
                .join('   ·   ');

            const perfLines = safeLines(doc.splitTextToSize(perf, contentWidth));
            doc.setTextColor(GRAY);
            doc.text(perfLines, margin, y);
            y += perfLines.length * 11 + 10;

            const body = resposta.linhas.map((linha) => [
                String(linha.metrica),
                String(linha.gemini),
                String(linha.openrouter)
            ]);

            doc.autoTable({
                startY: y,
                margin: { left: margin, right: margin },
                theme: 'grid',
                head: [[dados.rotuloMetrica, dados.rotuloGemini, dados.rotuloOpenRouter]],
                body,
                headStyles: { fillColor: [0, 120, 212], fontSize: 9, fontStyle: 'bold' },
                bodyStyles: { fontSize: 9, cellPadding: 3 },
                columnStyles: {
                    0: { cellWidth: contentWidth * 0.6 },
                    1: { cellWidth: contentWidth * 0.2, halign: 'center' },
                    2: { cellWidth: contentWidth * 0.2, halign: 'center' }
                }
            });

            y = doc.lastAutoTable.finalY + 14;

            const blocos = [
                { titulo: dados.rotuloFeedback, texto: resposta.feedbackGemini, cor: [0, 120, 212] },
                { titulo: dados.rotuloFeedback, texto: resposta.feedbackOpenRouter, cor: [120, 120, 120] },
                { titulo: dados.rotuloRecomendacao, texto: resposta.recomendacaoGemini, cor: [0, 120, 212] },
                { titulo: dados.rotuloRecomendacao, texto: resposta.recomendacaoOpenRouter, cor: [120, 120, 120] }
            ];

            blocos.forEach((bloco) => {
                if (!bloco.texto) return;

                doc.setFont('helvetica', 'bold');
                doc.setFontSize(9);
                novaPaginaSeNecessario(20);
                doc.setTextColor(bloco.cor[0], bloco.cor[1], bloco.cor[2]);
                doc.text(`${bloco.titulo}:`, margin, y);
                y += 12;

                doc.setFont('helvetica', 'normal');
                doc.setFontSize(8);
                doc.setTextColor(40, 40, 40);

                const textoLines = safeLines(doc.splitTextToSize(bloco.texto, contentWidth));
                doc.text(textoLines, margin, y);
                y += textoLines.length * 10 + 8;
            });

            y += 6;
        });

        y += 10;
    });

    const agora = new Date();
    const pad = (n) => String(n).padStart(2, '0');
    const nomeFicheiro =
        `Relatorio_Avaliacoes_${agora.getFullYear()}${pad(agora.getMonth() + 1)}${pad(agora.getDate())}_${pad(agora.getHours())}${pad(agora.getMinutes())}${pad(agora.getSeconds())}.pdf`;

    doc.save(nomeFicheiro);
};
