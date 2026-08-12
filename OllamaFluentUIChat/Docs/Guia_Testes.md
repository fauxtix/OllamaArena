# Guia de Testes — OllamaFluentUIChat

Checklist manual para validar o estado atual da aplicação (build + testes unitários + smoke test das funcionalidades).

## 0. Pré-requisitos

- `ollama serve` a correr (URL default `http://localhost:11434`; alterável em `appsettings.json` → `Ollama:BaseUrl`).
- Modelos locais disponíveis. Para testar o **reasoning**, a app deteta automaticamente (via `/api/show`) se o modelo suporta a capacidade `thinking` — use um modelo de reasoning (ex.: `ollama pull deepseek-r1`); com modelos normais o campo `think` é omitido e a app funciona normalmente.
- (Opcional) Chaves dos juízes em user-secrets (a partir de `OllamaFluentUIChat/`):
  - `dotnet user-secrets init`
  - `dotnet user-secrets set "ApiKeys:Gemini" "<chave>"`
  - `dotnet user-secrets set "ApiKeys:OpenRouter" "<chave>"`
  - Sem chaves, o juiz automático mostra um aviso (não bloqueia o resto da app).

## 1. Build + testes unitários

```
dotnet build OllamaFluentUIChat.slnx
dotnet test OllamaFluentUIChat.slnx
```

Esperado: **47 casos (43 métodos)** a passar:

| Projeto de teste | Cobre |
| --- | --- |
| `ScoreCalculatorTests` | renormalização de pesos, mínimo 8 métricas / pesos ≥ 50, HaloEffect com peso 0 |
| `EvaluationParserTests` | 12 métricas, prefixos hífen/asterisco, `LanguageConsistency` e `LoopDetection` |
| `ChatComposerServiceTests` | trim do histórico a 60%, fusão de mensagens, remoção do assistant final, `num_predict` ≤ contexto |
| `ChatMeasureTemperatureTests` | temperatura factual/criativa/código, remoção de acentos |
| `LocalAnalysisServiceTests` | listas vazias/nulas sem crash, modelo mais rápido, melhor avaliado, consenso |

## 2. Arranque e smoke test

- `dotnet watch run` em `OllamaFluentUIChat/` (perfil `https`).
- Confirmar que `ollama_benchmark.db` é criada com as **5 tabelas** (`Prompts`, `Respostas`, `Conversas`, `ConversaMensagens`, `Logs`) e sem erros no terminal.
- Menu lateral inclui **Dashboard** (`/dashboard`); `Settings2` e `AnaliseBenchmark` foram removidas (devem dar 404).

## 3. Chat (`/chat`)

- [ ] **Streaming + contexto**: enviar prompts; validar barra de contexto (tokens usados/restantes) e etiqueta de temperatura (≈0.3 para factuais).
- [ ] **Reasoning**: com modelo de reasoning, abrir o bloco "Raciocínio" (captura de `reasoning_content`); confirmar que persiste na conversa.
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
- [ ] **Avaliação manual**: preencher as 12 métricas + Overall, gravar e confirmar no Dashboard.

## 6. Dashboard (`/dashboard`)

- [ ] Com benchmarks gravados: cards (melhor modelo, nº de modelos, nº de avaliações) + ranking ordenado por score (progresso 0–5, colunas Gemini/OpenRouter).
- [ ] Com BD vazia: badge "Sem avaliações", sem crash.

## 7. Settings (`/settings`) e Logs (`/system-logs`)

- [ ] Tema/cor persistem em `localStorage` (chave `theme`); selecionar modelo guarda `ollama_model`.
- [ ] `/system-logs`: entradas Serilog (batch 100) gravadas na tabela `Logs` da BD.

## 8. Mobile

- [ ] Vista estreita (<768px): chat full-width, grids de avaliação empilham numa coluna, painel de histórico em overlay.
