# Guia: Apresentação de Screenshots (`OllamaArena.pptx`)

O repositório inclui a pasta **`OllamaArena/Screenshots/`** com **capturas de ecrã (PNG)** das principais páginas da aplicação e uma **apresentação PowerPoint gerada** (`OllamaArena.pptx`) que as organiza num deck de 16:9.

## 1. Conteúdo da pasta `Screenshots/`

| Item | Descrição |
|---|---|
| `*.png` | Capturas de ecrã da app (Home, Chat, Benchmarks, Qualidade e Métricas, Dashboard, Modelos, Prompts, Settings, Logs). Ordenadas por nome (ordem natural), cada uma numa slide com legenda limpa (prefixo numérico removido, `_` → espaço, separação camelCase — ex.: `2_Benchmarks_LocalAnalysis_1.png` → "Benchmarks Local Analysis 1"). |
| `OllamaArena.pptx` | **Apresentação gerada** a partir das capturas (ver estrutura abaixo). |
| `gerar_apresentacao.ps1` | Script PowerShell (COM do PowerPoint) que regenera o `OllamaArena.pptx` a partir das capturas presentes na pasta. |

> A imagem `wwwroot/images/settings-ollama.png` **não** é incluída na apresentação (apenas as duas imagens de introdução listadas na secção 2).

## 2. Estrutura do deck (16:9 — 960 × 540 pt)

1. **Slide de título** — "OllamaArena - Screenshots" + subtítulo com o nº de capturas.
2. **2 slides de introdução** com as imagens de marca da pasta `wwwroot/images`:
   - `OllamaWithFluentUI.jpg` → "Ollama com FluentUI";
   - `OllamaAsAJudge.png` → "Ollama como Juiz".
3. **1 slide por screenshot** — imagem centrada (proporção mantida) + legenda no rodapé.

## 3. Template, transições e avanço

- **Template profissional IT (desenhado por código):** fundo com gradiente azul-escuro (`#0B1E3D → #142F5C`), barra de acento Fluent `#0078D4` no topo e rodapé com "OllamaArena" + número do slide.
- **Transições modernas (Office 2016+):** Fade na abertura; nos screenshots, um ciclo de efeitos modernos (Vortex, Switch, Glitter Diamond, Shred, Ripple, Fly Through, Honeycomb, Ferris Wheel, Reveal, Gallery, Conveyor, Doors, Window, Warp, Cube), ~1 s cada. Se um efeito não for suportado, o script aplica automaticamente Fade como fallback.
- **Avanço:** clique/tecla **ou** automático após **10 s** (`AdvanceOnClick` + `AdvanceOnTime` em todas as slides).

## 4. Como regenerar

**Pré-requisitos:** Windows com **PowerPoint 2016+** instalado (o script usa automação COM `PowerPoint.Application`).

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "OllamaArena\Screenshots\gerar_apresentacao.ps1"
```

O script:

1. Lê os `*.png` da própria pasta (ordenados por ordem natural) e as duas imagens de introdução em `OllamaArena/wwwroot/images`;
2. Constrói o deck (título → intros → screenshots) aplicando o template, as transições e o avanço 10 s;
3. Grava/sobrescreve `OllamaArena.pptx` na mesma pasta;
4. Reabre o ficheiro em modo só-leitura e imprime a verificação (`slides=… tamanho=… bytes`), fechando o PowerPoint no fim (sem processos órfãos).

> A janela do PowerPoint aparece brevemente durante a geração (requisito da automação COM). Depois de adicionar novas capturas à pasta, basta correr o script novamente para regenerar o deck (o título é atualizado com o novo nº de capturas).
