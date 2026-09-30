# Fontes embebidas no relatório PDF

Ficheiros em `wwwroot/fonts/` usados por `wwwroot/js/pdf.js` (export PDF de
`/benchmark-evaluations`). São **instâncias estáticas** em subset latino, porque o
jsPDF não consegue ler fontes variáveis (`*VF`/opsz) e precisa das métricas reais
para calcular o `wrapping`.

| Ficheiro | Família | Estilo | Origem |
|---|---|---|---|
| `Inter-Regular.ttf` | Inter | normal | Google Fonts, `css?family=Inter:400` (v20) |
| `Inter-Bold.ttf` | Inter | bold | Google Fonts, `css?family=Inter:700` (v20) |
| `RobotoMono-Regular.ttf` | Roboto Mono | normal | Google Fonts, `css?family=Roboto+Mono:400` (v31) |

**Porquê embebê-las:** as 14 standard fonts do PDF (`helvetica`, `courier`, ...) medem o
texto com uma tabela de métricas aproximada, indexada por `charCode`. Medido com o jsPDF
2.x a 9pt, e para além do glifo não existir na fonte:

| Caractere | helvetica | courier | Inter (TTF) |
|---|---|---|---|
| `—` (em dash) | 9,00 pt | 5,40 pt | 9,00 pt (1,0 em) |
| `–` (en dash) | 4,95 pt | 5,40 pt | 4,50 pt |
| `”` (aspa curva) | 2,97 pt | 5,40 pt | 3,96 pt |
| `€` | 4,77 pt | 5,40 pt | 6,00 pt |
| `→` `✓` CJK (fora da fonte) | 4,77 pt (largura genérica) | 5,40 pt | glifo real ou removido |

Ou seja: o `courier` mede qualquer caractere a 0,6 em (logo o prompt nunca é medido com
métricas reais) e a `helvetica` dá a mesma largura a caracteres diferentes — a quebra de
linha é calculada para um texto diferente do que é desenhado e as linhas saem da margem
direita. Com as TTF embebidas as medições vêm do próprio ficheiro e o PDF usa a mesma
tipografia da UI. Sem elas o `pdf.js` continua a funcionar em `helvetica`/`courier`.

**Cobertura:** são subsets "latin" (ASCII, Latin-1, ı/œ/Œ, aspas tipográficas, `–`, `—`,
`•`, `…`, `€`, `™`, `−`, acento combinante acute). Escrita noutros alfabetos (cirílico,
grego, CJK) e símbolos fora desta lista são removidos pelo `normalizarTextoPdf` para não
sairem como caixas (`.notdef`); o allowlist `CARACTERES_COBERTOS` do `pdf.js` foi escrito
a partir da tabela `cmap` destes ficheiros — ao trocar de fonte, tem de ser revisto.

**Licença:** ambas as famílias são **SIL Open Font License 1.1** (ver `OFL.txt`).
Inter substitui o Segoe UI Variable da UI (tipografia muito próxima da do Fluent UI);
Roboto Mono substitui a pilha `ui-monospace`/`Consolas` usada na coluna
"Descrição" da grelha.
