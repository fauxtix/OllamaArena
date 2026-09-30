// Relatório PDF das avaliações dos juiz (BenchmarkEvaluations -> ExportarParaPdfAsync).
//
// Nota sobre as fontes: as 14 "standard fonts" do PDF (helvetica, courier, ...) medem
// o texto com uma tabela de métricas aproximada, indexada por charCode. O que está fora
// da cobertura da fonte (→ ✓ € CJK, aspas curvas...) é medido com a largura de um
// caractere genérico e sai como .notdef (caixa vazia), pelo que a quebra de linha é
// calculada para um texto diferente do desenhado e as linhas "furam" a margem.
// O courier é ainda pior: mede qualquer caractere a 0,6 em, incluindo o prompt.
// Por isso embebemos TTFs Unicode reais (subset latino) servidos de /fonts, cujas
// métricas vêm do próprio ficheiro.
const FONTES_PDF = [
    { ficheiro: 'Inter-Regular.ttf', familia: 'Inter', estilo: 'normal' },
    { ficheiro: 'Inter-Bold.ttf', familia: 'Inter', estilo: 'bold' },
    { ficheiro: 'RobotoMono-Regular.ttf', familia: 'RobotoMono', estilo: 'normal' }
];

// Carregadas uma única vez e mantidas em base64 (evita novo fetch a cada exportação).
let _fontesPdfBase64 = null;
let _fontesPdfFalhou = false;

const _bytesParaBase64 = (buffer) => {
    const bytes = new Uint8Array(buffer);
    let binario = '';
    const bloco = 0x8000;
    for (let i = 0; i < bytes.length; i += bloco) {
        binario += String.fromCharCode.apply(null, bytes.subarray(i, i + bloco));
    }
    return btoa(binario);
};

// Relativo ao <base href> da app: funciona também em virtual directory do IIS.
const _urlDaFonte = (ficheiro) => new URL('fonts/' + ficheiro, document.baseURI).href;

const carregarFontesPdf = async () => {
    if (_fontesPdfBase64 || _fontesPdfFalhou) return _fontesPdfBase64;

    try {
        const base64 = {};
        for (const fonte of FONTES_PDF) {
            const resposta = await fetch(_urlDaFonte(fonte.ficheiro), { cache: 'force-cache' });
            if (!resposta.ok) throw new Error(`${fonte.ficheiro}: HTTP ${resposta.status}`);
            base64[fonte.ficheiro] = _bytesParaBase64(await resposta.arrayBuffer());
        }
        _fontesPdfBase64 = base64;
    } catch (erro) {
        // Sem fontes embebidas o relatório sai na helvetica (wrapping imperfeito),
        // mas a exportação nunca deve falhar por causa das fontes.
        console.warn('[pdf] não foi possível carregar as fontes, a usar helvetica.', erro);
        _fontesPdfFalhou = true;
    }

    return _fontesPdfBase64;
};

const registarFontesPdf = (doc) => {
    if (!_fontesPdfBase64) return false;
    for (const fonte of FONTES_PDF) {
        const nome = fonte.familia + '-' + fonte.estilo + '.ttf';
        doc.addFileToVFS(nome, _fontesPdfBase64[fonte.ficheiro]);
        doc.addFont(nome, fonte.familia, fonte.estilo);
    }
    return true;
};

// Caracteres realmente presentes no subset latino das três fontes (verificado contra a
// tabela cmap dos TTF em wwwroot/fonts): ASCII, Latin-1 (sem o hífen opcional), ı/œ/Œ,
// aspas tipográficas, – —, •, …, €, ™, o hífen/menos e o acento combinante acute.
// As fontes são subset "latin": escrita noutros alfabetos (cirílico, grego, CJK) fica de
// fora, pelo que esses caracteres são removidos em vez de saírem como caixas (.notdef).
const CARACTERES_COBERTOS =
    /[\u0020-\u007E\u00A1-\u00AC\u00AE-\u00FF\u0131\u0152\u0153\u02C6\u02DA\u02DC\u0301\u2013\u2014\u2018\u201A\u201C-\u201E\u2022\u2026\u20AC\u2122\u2212]/;

// Caracteres convertidos em ASCII equivalente (o que não estiver aqui e não for coberto
// pelas fontes é simplesmente removido).
const SIMBOLOS_PARA_ASCII = {
    '←': '<-', '↑': '^', '→': '->', '↓': 'v', '↔': '<->', '↕': '|^',
    '⇒': '=>', '⇐': '<=', '⟹': '=>', '⇔': '<=>',
    '↻': 'x', '⟳': 'x', '↺': 'x', '↩': '<-', '↪': '->',
    '⤷': '->', '↳': '->', '⤴': '->',
    '≤': '<=', '≥': '>=', '≠': '!=', '≈': '~', '±': '+/-',
    '⁄': '/', '‐': '-', '‑': '-', '‒': '-',
    '√': 'v', '∑': 'S', '∏': 'P', '∞': 'oo', '∂': 'd',
    '†': '*', '‡': '**', '●': '-', '▪': '-', '‣': '-', '⁃': '-',
    '✓': 'v', '✔': 'v', '✗': 'x', '✘': 'x', '⚠': '!', '★': '*', '☆': '*',
    '‰': '%', '′': "'", '″': '"', '〞': '"', '〟': '"', 'ʼ': "'", '＂': '"'
};

// Espaços exóticos (todos presentes na fonte só como espaço normal ou nem existem):
// medem-se como espaço para a quebra de linha ficar previsível.
const ESPACOS_EXOTICOS = '\u00A0\u2000\u2001\u2002\u2003\u2004\u2005\u2006\u2007\u2008\u2009\u200A\u202F\u205F\u3000';
// Invisíveis: removidos porque falseiam a medição (largura 0) ou mudam a direcção do texto.
const INVISIVEIS = '\u00AD\u200B\u200C\u200D\u200E\u200F\u202A\u202B\u202C\u202D\u202E\u2060\u2066\u2067\u2068\u2069\uFEFF';

const normalizarTextoPdf = (texto) => {
    if (texto === null || texto === undefined) return '';

    // NFC compõe "e" + acento num único glifo (as fontes só têm as formas pré-compostas)
    // e transforma os separadores de linha Unicode em \n antes da limpeza.
    let saida = String(texto);
    if (saida.normalize) saida = saida.normalize('NFC');

    return saida
        .replace(/[\r\u2028\u2029]+/g, '\n')
        .replace(/[\u0000-\u0008\u000B\u000C\u000E-\u001F\u007F]/g, '')
        .split('\n')
        .map(linha => Array.from(linha).map((c) => {
            if (CARACTERES_COBERTOS.test(c)) return c;
            if (ESPACOS_EXOTICOS.indexOf(c) >= 0) return ' ';
            if (INVISIVEIS.indexOf(c) >= 0) return '';
            return Object.prototype.hasOwnProperty.call(SIMBOLOS_PARA_ASCII, c) ? SIMBOLOS_PARA_ASCII[c] : '';
        }).join(''))
        .join('\n');
};

window.gerarRelatorioPDF = async function (dados) {

    // Construtor: os builds UMD expõem window.jspdf.jsPDF, os builds mais antigos
    // window.jsPDF. Aceitamos os dois para não depender da variante vendorizada.
    const jsPDFConstrutor = (window.jspdf && window.jspdf.jsPDF) || window.jsPDF;
    if (!jsPDFConstrutor) {
        throw new Error("jsPDF não carregado.");
    }
    const comAutoTable = !!(jsPDFConstrutor.API && jsPDFConstrutor.API.autoTable);
    if (!comAutoTable) {
        console.warn("[pdf] plugin autoTable indisponível: a tabela de métricas não será gerada.");
    }

    await carregarFontesPdf();

    const doc = new jsPDFConstrutor({ orientation: 'landscape', unit: 'pt', format: 'a4' });
    const comFontes = registarFontesPdf(doc);

    const SANS = comFontes ? 'Inter' : 'helvetica';
    const MONO = comFontes ? 'RobotoMono' : 'courier';

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

    // Quebra de página: tem de ser chamada ANTES de cada linha, caso contrário um
    // parágrafo longo continua para fora da página e é cortado.
    const novaPaginaSeNecessario = (alturaNecessaria) => {
        if (y + alturaNecessaria > pageHeight - margin) {
            doc.addPage();
            y = margin + 20;
        }
    };

    // Escreve um parágrafo já partido em linhas, paginando linha a linha.
    const escreverParagrafo = (texto, opcoes) => {
        const o = opcoes || {};
        const tamanho = o.tamanho || 9;
        const alturaLinha = o.alturaLinha || Math.round(tamanho * 1.2);
        const normalizado = normalizarTextoPdf(texto);
        if (!normalizado.trim()) return 0;

        doc.setFont(o.fonte || SANS, o.estilo || 'normal');
        doc.setFontSize(tamanho);
        if (o.cor) doc.setTextColor(o.cor[0], o.cor[1], o.cor[2]);

        const linhas = safeLines(doc.splitTextToSize(normalizado, o.largura || contentWidth));
        linhas.forEach(linha => {
            novaPaginaSeNecessario(alturaLinha);
            doc.text(linha, margin, y);
            y += alturaLinha;
        });
        y += (o.espacoFinal === undefined ? 6 : o.espacoFinal);

        return linhas.length;
    };

    // Rótulo bold (Feedback:, Recomendação:, Prompt #id, modelo...) que não pode ficar
    // órfão no fim da página: garante espaço para ele e para as linhas seguintes.
    const escreverTitulo = (texto, tamanho, cor, espacoSeguir) => {
        doc.setFont(SANS, 'bold');
        doc.setFontSize(tamanho);
        doc.setTextColor(cor[0], cor[1], cor[2]);
        novaPaginaSeNecessario((espacoSeguir === undefined ? 22 : espacoSeguir) + tamanho * 1.2);
        doc.text(normalizarTextoPdf(texto), margin, y);
        y += tamanho * 1.2;
    };

    doc.setFont(SANS, 'bold');
    doc.setFontSize(18);
    doc.setTextColor(BLUE);
    doc.text(String(normalizarTextoPdf(dados.titulo)), margin, y + 30);
    y += 52;

    doc.setFont(SANS, 'normal');
    doc.setFontSize(9);
    doc.setTextColor(GRAY);
    doc.text(normalizarTextoPdf(`${dados.geradoEm}: ${dados.data}`), margin, y);
    y += 26;

    dados.grupos.forEach((grupo) => {

        escreverTitulo(`${dados.rotuloPrompt} #${grupo.id}`, 12, [0, 0, 0], 20);

        // O prompt vai em monoespaçada, tal como a coluna "Descrição" da grelha.
        escreverParagrafo(grupo.prompt, { fonte: MONO, tamanho: 9, espacoFinal: 6 });

        escreverParagrafo(`${dados.rotuloData}: ${grupo.data}`, { tamanho: 8, cor: [74, 74, 74], espacoFinal: 8 });

        grupo.respostas.forEach((resposta) => {

            escreverTitulo(`${dados.rotuloModelo}: ${resposta.modelo}`, 11, [0, 0, 0], 20);

            const perf = dados.rotulosDesempenho
                .map((r, i) => `${r}: ${resposta.desempenho[i]}`)
                .join('   ·   ');

            escreverParagrafo(perf, { tamanho: 9, cor: [74, 74, 74], espacoFinal: 10 });

            const body = resposta.linhas.map((linha) => [
                normalizarTextoPdf(linha.metrica),
                normalizarTextoPdf(linha.gemini),
                normalizarTextoPdf(linha.openrouter)
            ]);

            if (comAutoTable) {
                doc.autoTable({
                    startY: y,
                    margin: { left: margin, right: margin },
                    theme: 'grid',
                    head: [[dados.rotuloMetrica, dados.rotuloGemini, dados.rotuloOpenRouter].map(normalizarTextoPdf)],
                    body,
                    styles: { font: SANS, fontSize: 9, cellPadding: 3 },
                    headStyles: { font: SANS, fontSize: 9, fontStyle: 'bold', fillColor: [0, 120, 212] },
                    columnStyles: {
                        0: { cellWidth: contentWidth * 0.6 },
                        1: { cellWidth: contentWidth * 0.2, halign: 'center' },
                        2: { cellWidth: contentWidth * 0.2, halign: 'center' }
                    }
                });

                y = doc.lastAutoTable.finalY + 14;
            }

            // Rótulo por resposta, com a origem (API automática ou juiz externo); o
            // fallback é o rótulo do slot quando a origem não veio (registo legado).
            const juiz1 = resposta.rotuloJuiz1 || dados.rotuloGemini;
            const juiz2 = resposta.rotuloJuiz2 || dados.rotuloOpenRouter;
            const blocos = [
                { titulo: `${dados.rotuloFeedback} · ${juiz1}`, texto: resposta.feedbackGemini, cor: [0, 120, 212] },
                { titulo: `${dados.rotuloFeedback} · ${juiz2}`, texto: resposta.feedbackOpenRouter, cor: [120, 120, 120] },
                { titulo: `${dados.rotuloRecomendacao} · ${juiz1}`, texto: resposta.recomendacaoGemini, cor: [0, 120, 212] },
                { titulo: `${dados.rotuloRecomendacao} · ${juiz2}`, texto: resposta.recomendacaoOpenRouter, cor: [120, 120, 120] }
            ];

            blocos.forEach((bloco) => {
                if (!bloco.texto) return;

                escreverTitulo(`${bloco.titulo}:`, 9, bloco.cor, 20);

                escreverParagrafo(bloco.texto, { tamanho: 8, cor: [40, 40, 40], espacoFinal: 8 });
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
