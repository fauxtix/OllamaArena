// Intercepta cliques em âncoras internas (#fragmento) geradas pelo Markdown
// (índice dos planos de teste, README, etc.). Sem isto, o Blazor trata o clique
// como navegação interna e recarrega a página, fechando o conteúdo apresentado.
// Corre em fase de captura para chegar antes do handler de navegação do Blazor.
(function () {
    'use strict';

    function normalizarId(texto) {
        return texto
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '')
            .toLowerCase();
    }

    function decodificarFragmento(hash) {
        try {
            return decodeURIComponent(hash);
        } catch (e) {
            return hash;
        }
    }

    document.addEventListener('click', function (e) {
        var link = e.target && e.target.closest ? e.target.closest('a[href^="#"]') : null;
        if (!link) {
            return;
        }

        var fragmento = decodificarFragmento(link.getAttribute('href').slice(1));

        // "#": volta ao topo da página
        if (fragmento === '') {
            e.preventDefault();
            e.stopPropagation();
            window.scrollTo({ top: 0, behavior: 'smooth' });
            return;
        }

        // Procura por id exato; se não existir, compara ids normalizados
        // (diacríticos removidos) — cobre links escritos com acentos.
        var alvo = document.getElementById(fragmento);
        if (!alvo) {
            var esperado = normalizarId(fragmento);
            var candidatos = document.querySelectorAll('[id]');
            for (var i = 0; i < candidatos.length; i++) {
                if (normalizarId(candidatos[i].id) === esperado) {
                    alvo = candidatos[i];
                    break;
                }
            }
        }
        if (!alvo) {
            return;
        }

        e.preventDefault();
        e.stopPropagation();
        alvo.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, true);
})();
