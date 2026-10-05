// Descarga do .xlsx de benchmarks.
//
// O ficheiro é servido por um endpoint HTTP e não pelo circuito SignalR: em base64
// via JS interop o payload contava com o limite de 32 MB do circuito e a exportação
// rebentava para ficheiros grandes. Aqui o browser pede o ficheiro por fetch e
// guarda-o a partir de um Blob, o payload em memória deixa de ser serializado
// como texto JSON.
window.downloadBenchmarksExcel = async (url, fileName) => {
    const resposta = await fetch(url);

    if (!resposta.ok) {
        throw new Error(`Falha ao descarregar a folha de cálculo (HTTP ${resposta.status}).`);
    }

    const blob = await resposta.blob();
    const objectUrl = URL.createObjectURL(blob);

    try {
        const link = document.createElement('a');
        link.download = fileName;
        link.href = objectUrl;
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
    } finally {
        // O Blob continua referenciado enquanto o browser não descarregar o ficheiro.
        URL.revokeObjectURL(objectUrl);
    }
}
