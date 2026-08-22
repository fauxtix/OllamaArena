# Guia de Testes — OllamaArena

Checklist manual para validar o estado atual da aplicação (build + testes unitários + smoke test das funcionalidades).

## 0. Pré-requisitos

- `ollama serve` a correr (URL default `http://localhost:11434`; alterável em `appsettings.json` → `Ollama:BaseUrl`).
- Modelos locais disponíveis. Para testar o **reasoning**, a app deteta automaticamente (via `/api/show`) se o modelo suporta a capacidade `thinking` — use um modelo de reasoning (ex.: `ollama pull deepseek-r1`) e **ative o toggle "Ativar reasoning (think)" na página Settings** (default é **desligado**); com modelos normais o campo `think` é omitido e a app funciona normalmente.
- (Opcional) Chaves dos juízes em user-secrets (a partir de `OllamaArena/`):
  - `dotnet user-secrets init`
  - `dotnet user-secrets set "ApiKeys:Gemini" "<chave>"`
  - `dotnet user-secrets set "ApiKeys:OpenRouter" "<chave>"`
  - Sem chaves, o juiz automático mostra um aviso (não bloqueia o resto da app).
- (Opcional, **deploy IIS**) Em IIS o user-secrets não é lido (só em Development). Acrescente as chaves ao `appsettings.Local.json` no servidor (ex.: `C:\inetpub\wwwroot\OllamaArena\appsettings.Local.json`):
  ```json
  {
    "Security": { "EnableHttpsRedirection": false },
    "ApiKeys": { "Gemini": "<chave>", "OpenRouter": "<chave>" }
  }
  ```
  O ficheiro é preservado pelo workflow de deploy e relido em runtime (`reloadOnChange`). Manter o bloco `Security` (escrito automaticamente pelo workflow). Alternativa: variáveis de ambiente no App Pool (`ApiKeys__Gemini` / `ApiKeys__OpenRouter`).

## 1. Build + testes unitários

```
dotnet build OllamaArena.slnx
dotnet test OllamaArena.slnx
```

Esperado: **todos os testes passam** (93 testes em 8 classes — `dotnet test OllamaArena.slnx` informa o total):

| Projeto de teste | Cobre |
| --- | --- |
| `ScoreCalculatorTests` | renormalização de pesos, mínimo 8 métricas / pesos ≥ 50, HaloEffect com peso 0 |
| `EvaluationParserTests` | 12 métricas, prefixos hífen/asterisco, `LanguageConsistency` e `LoopDetection`, cultura pt |
| `ChatComposerServiceTests` | trim do histórico a 60%, fusão de mensagens, remoção do assistant final, `num_predict` ≤ contexto, reasoning |
| `ChatMeasureTemperatureTests` | temperatura factual/criativa/código, remoção de acentos |
| `LocalAnalysisServiceTests` | listas vazias/nulas sem crash, modelo mais rápido, melhor avaliado, consenso |
| `SqliteTypeHandlersTests` | conversão de `INTEGER`/`REAL`/`NULL` do SQLite para `double`/`float`/nullable sem `InvalidCastException` |
| `MessageFormatterTests` | resgate de fences coladas (C#/JS/Python), quebra de código de linha única, `StripTrailingTag`, mascaramento e normalizações |
| `OpenRouterCatalogServiceTests` | catálogo ao vivo do OpenRouter (modelos gratuitos, `openrouter/free` pinado no topo) |

## 2. Arranque e smoke test

- `dotnet watch run` em `OllamaArena/` (perfil `https`).
- Confirmar que `ollama_benchmark.db` é criada com as **7 tabelas** (`Prompts`, `Respostas`, `Conversas`, `ConversaMensagens`, `Configuracoes`, `ModelosJuiz`, `Logs`) e sem erros no terminal.
- Menu lateral inclui **Dashboard** (`/dashboard`); `Settings2` e `AnaliseBenchmark` foram removidas (devem dar 404).

## 3. Chat (`/chat`)

- [ ] **Streaming + contexto**: enviar prompts; validar barra de contexto (tokens usados/restantes) e etiqueta de temperatura (≈0.3 para factuais).
- [ ] **Guardar automático**: após a resposta do modelo, confirmar que a conversa é gravada na BD sem diálogos de confirmação; verificar que a descrição do prompt contém o SUMMARY gerado pelo modelo (ou frase de fallback se o SUMMARY não estiver presente).
- [ ] **Reasoning**: com modelo de reasoning **e o toggle "Ativar reasoning (think)" ligado na Settings** (default é desligado), abrir o bloco "Raciocínio" (captura de `reasoning_content`); confirmar que persiste na conversa. Com o toggle desligado, modelos tipo `deepseek-r1` continuam lentos mas o bloco não aparece nem o reasoning é gravado.
- [ ] **Cancelar**: durante o streaming, cancelar e confirmar descarga da VRAM (`keep_alive=0` em `/api/generate`).
- [ ] **Novo Chat**: reset do ID de conversa + descarga da VRAM.
- [ ] **Web search**: ligar o toggle e perguntar algo factual — Wikipedia deve devolver contexto; DuckDuckGo pode vir vazio (anti-bot, comportamento esperado).

## 4. Conversas (histórico)

- [ ] Após trocar mensagens, abrir **Histórico** → tab **Conversas**: título (60 chars), modelo e data.
- [ ] **Carregar** uma conversa: mensagens (incl. reasoning, temperatura e tempo) repostas no chat.
- [ ] **Exportar** JSON (`conversas_yyyyMMdd_HHmmss.json`) e **Importar** o mesmo ficheiro → nova conversa na lista.

## 5. Benchmarks (`/benchmarks` e `/benchmark-evaluations`)

- [ ] Respostas do chat gravadas como benchmarks (badges `Tokens/s`, `Eval`, `Load`, `Tokens`).
- [ ] **Avaliar automaticamente** num cartão:
  - Sem internet → erro `Benchmarks.NoInternetError`;
  - Sem chaves → aviso de chave não configurada;
  - Com chaves → diálogo preenchido com **12 métricas** por juiz.
- [ ] **Quota**: segunda avaliação seguida → `QuotaLimitMessage` (intervalo default 60s). Falhas não marcam quota (novas tentativas permitidas de imediato).
- [ ] **Copiar prompt** (fallback manual, funciona sem internet).
- [ ] **Falha parcial de juiz**: se um dos juízes falhar (ex.: `Too many requests`/tráfego), confirmar que o aviso identifica qual falhou, que os campos desse juiz ficam editáveis e que dá para: esperar e tentar mais tarde, aproveitar a avaliação do outro juiz e preencher o que falta manualmente, ou fazer toda a avaliação à mão.
- [ ] **Avaliação manual**: preencher as 12 métricas + Overall, gravar e confirmar no Dashboard.
- [ ] **Análise IA Local**: com benchmarks avaliados, abrir a Análise IA Local e confirmar que a secção "Métricas Fracas" lista métricas com nota ≤3 (≤2 para Segurança) agrupadas por modelo.
- [ ] **Apagar com âmbitos** em `/benchmark-evaluations`: menu "Apagar Benchmarks" com 4 opções (filtrados / por avaliar / avaliados / todos) + contagens; âmbitos vazios desativados; confirmação indica o nº de respostas; após apagar, a grelha atualiza.
- [ ] **Gráficos por prompt**: abrir o diálogo de gráficos e validar as tabs **Desempenho** (4 gráficos de barras com download) e **Qualidade** (2 radares Gemini/OpenRouter na escala 0–5; sem avaliações de um juiz → "Sem avaliações para este juiz.").
- [ ] **Métricas fracas na avaliação**: com avaliações gravadas, abrir o detalhe de uma avaliação e confirmar que as células com nota ≤3 (≤2 para Segurança) têm fundo vermelho (`rating-cell-weak`).
- [ ] **Guarda de idioma na avaliação**: com a UI em PT, avaliar automaticamente uma resposta claramente escrita em inglês — confirmar que **Language Consistency fica ≤2 nos dois juízes** e que a recomendação começa por "[Verificação local] Resposta detetada em inglês…". Com resposta no idioma certo (ou deteção inconclusiva), o guard não interfere.

## 6. Dashboard (`/dashboard`)

- [ ] Com benchmarks gravados: **6 KPI cards** (Melhor Modelo com score, Modelos avaliados, Cobertura de avaliação em % com fração "x/y respostas com os 2 juízes", Prompts testados com data do último benchmark, Modelo mais rápido com tokens/s, Maior divergência juízes com valor Δ) + **2 radares de qualidade** (Gemini/OpenRouter, Chart.js, escala 0–5, 12 métricas, com download de imagem) + **1 scatter plot** (tokens/s vs. score, com download) + **1 gráfico de linhas "Evolução do score ao longo do tempo"** (média acumulada por modelo, legenda por modelo, com download) + **tabela classificativa** ordenável (Posição, Modelo, Score com barra 0–5, Gemini, OpenRouter, Tokens/s, Tempo médio, Divergência — "—" se só um juiz avaliou, Tokens/resposta).
- [ ] Sem BD vazia: badge "Sem avaliações", sem crash.
- [ ] Sem avaliações de um juiz (ex.: só Gemini): radar desse juiz mostra "Sem avaliações para este juiz." sem erro.

## 7. Settings (`/settings`) e Logs (`/system-logs`)

- [ ] **Chaves de API**: definir chaves Gemini/OpenRouter, guardar — confirmar que o botão só fica ativo com alterações (dirty-tracking) e que abre diálogo de confirmação com resumo (chaves mostradas como "definida/alterada" ou "removida", nunca o valor).
- [ ] **Modelo OpenRouter**: catálogo ao vivo (radio buttons), `openrouter/free` fixo no topo; campo "ou escreve um ID à mão" para offline/custom; pesquisa no catálogo.
- [ ] **Reasoning toggle**: ligar/desligar o FluentSwitch "Ativar reasoning (think)" — confirmar que `Chat:EnableReasoning` é gravado em `Configuracoes` e que o chat envia `think: true`/`false` apropriado (modelos com capacidade `thinking` via `/api/show`).
- [ ] Tema/cor persistem em `localStorage` (chave `theme`); selecionar modelo guarda `ollama_model`.
- [ ] `/system-logs`: entradas Serilog (batch 100) gravadas na tabela `Logs` da BD.

## 8. Mobile

- [ ] Vista estreita (<768px): chat full-width, grids de avaliação empilham numa coluna, painel de histórico em overlay.
