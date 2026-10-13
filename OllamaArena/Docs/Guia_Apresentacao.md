# Guia: Apresentações de Screenshots (`OllamaArena.pptx` e `OllamaArena_IA.pptx`)

O repositório inclui a pasta **`OllamaArena/Screenshots/`** com **capturas de ecrã (PNG)** das principais páginas da aplicação e **duas apresentações PowerPoint geradas** que as organizam em decks de 16:9:

| Deck | Origem das imagens | Script |
|---|---|---|
| `OllamaArena.pptx` | Capturas de ecrã reais da app (`.png` na raiz de `Screenshots/`) | `gerar_apresentacao.ps1` |
| `OllamaArena_IA.pptx` | Ilustrações geradas por IA (`AI_Generated/OllamaArena-*.png`) | `gerar_apresentacao_IA.ps1` |

## 1. Conteúdo da pasta `Screenshots/`

| Item | Descrição |
|---|---|
| `*.png` | Capturas de ecrã da app (Home, Chat, Benchmarks, Qualidade e Métricas, Dashboard, Modelos, Prompts, Settings, Logs). Ordenadas por nome (ordem natural), cada uma numa slide com legenda limpa (prefixo numérico removido, `_` → espaço, separação camelCase — ex.: `2_Benchmarks_LocalAnalysis_1.png` → "Benchmarks Local Analysis 1"). O prefixo `N_` **não é obrigatório**: capturas como `Chat - History - 1.png` e `ModelosGuardados_Ajuda.png` usam o próprio nome como base da legenda (apenas `_` → espaço e separação camelCase; a ordenação usa o último número do nome). |
| `AI_Generated/OllamaArena-01.png` … `-09.png` | Ilustrações usadas apenas pela apresentação IA (ver secção 6). Não entram no `OllamaArena.pptx`. |
| `OllamaArena.pptx` | **Apresentação gerada** a partir das capturas de ecrã (ver estrutura abaixo). |
| `OllamaArena_IA.pptx` | **Apresentação gerada** a partir das ilustrações de `AI_Generated/`. |
| `gerar_apresentacao.ps1` | Script PowerShell (COM do PowerPoint) que regenera o `OllamaArena.pptx` a partir das capturas presentes na pasta. |
| `gerar_apresentacao_IA.ps1` | Script PowerShell que regenera o `OllamaArena_IA.pptx` a partir de `AI_Generated/`. |

> A imagem `wwwroot/images/settings-ollama.png` **não** é incluída na apresentação (apenas as duas imagens de introdução listadas na secção 2).

> Fora desta pasta existe ainda **`OllamaArena/Docs/OllamaAsAJudge.pptx`**, um deck conceptual sobre a arquitetura "Ollama como juiz". É um artefacto independente, sem script de regeneração: edite-o manualmente no PowerPoint.

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

## 5. Os briefs por trás das ilustrações de `AI_Generated/`

As 9 ilustrações de `Screenshots/AI_Generated/` não são geradas por um script: são o resultado de briefs escritos à mão, guardados em **`OllamaArena/Docs/Prompts/`** (um ficheiro `.txt` por imagem, `01-inicio.txt` … `09-definicoes.txt`). Cada brief tem um cabeçalho com a página e a rota que ilustra, uma linha de contexto, e a partir de `PROMPT — copie a partir daqui para o gerador de imagens:` o texto exato a colar no gerador de imagens (paleta, painéis, etiquetas). Para alterar ou recriar uma ilustração:

1. abrir o brief correspondente em `OllamaArena/Docs/Prompts/`;
2. usar o prompt tal como está no gerador de imagens escolhido;
3. gravar o resultado como `Screenshots/AI_Generated/OllamaArena-NN.png` (**numeração sequencial obrigatória** — o script ordena pelo número final do nome).

> Estes briefs **não** são os prompts runtime da aplicação: os prompts editáveis em runtime vivem em `OllamaArena/Prompts/`, não em `Docs/Prompts/`.

## 6. Como regenerar a apresentação IA

**Pré-requisitos:** as ilustrações já existirem em `Screenshots/AI_Generated/` (ver secção 5). O script desenha as formas e o texto no próprio PowerPoint, pelo que **também** requer Windows com **PowerPoint 2016+**.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "OllamaArena\Screenshots\gerar_apresentacao_IA.ps1"
```

Diferenças face ao script das capturas de ecrã:

- **Tema "Computing" / editor de código** (fundo `#0D1117 → #161B22`, acento ciano `#00E5FF`, secundários verde `#3FB950`, roxo `#BC8CFF`, laranja `#D29922` e azul de código `#58A6FF`), em vez do template azul-escuro com acento Fluent;
- lê **apenas** `AI_Generated/OllamaArena-*.png`, ordenadas pelo número, ignorando os `.png` da raiz de `Screenshots/`;
- estrutura: **capa + 1 slide por ilustração + slide de fecho**;
- se `OllamaArena_IA.pptx` estiver aberto (em uso), grava em `OllamaArena_IA_v2.pptx`, `OllamaArena_IA_v3.pptx`, … em vez de falhar.

## 7. Decks de documentação em `OllamaArena/Docs/`

Além dos dois decks de capturas em `Screenshots/`, existem **dois decks de documentação** em `OllamaArena/Docs/`, ambos gerados por scripts PowerShell com o mesmo template visual e regidos pelos mesmos pré-requisitos (Windows + PowerPoint 2016+):

| Deck | Origem | Script |
|---|---|---|
| `Funcionalidades.pptx` | `OllamaArena/Docs/Funcionalidades.md` | `OllamaArena/Docs/gerar_funcionalidades.ps1` |
| `Guia_Comparar_Modelos.pptx` | `OllamaArena/ComoCompararModelos/Guia_Como_Comparar_Modelos.md` | `OllamaArena/Docs/gerar_guia_como_comparar.ps1` |

Para regenerar um deles, corra o script a partir de `OllamaArena/Docs/` (ou com o caminho completo):

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "OllamaArena\Docs\gerar_funcionalidades.ps1"
powershell -NoProfile -ExecutionPolicy Bypass -File "OllamaArena\Docs\gerar_guia_como_comparar.ps1"
```

O `OllamaArena/Docs/OllamaAsAJudge.pptx` continua a ser um artefacto manual, sem script de regeneração.
