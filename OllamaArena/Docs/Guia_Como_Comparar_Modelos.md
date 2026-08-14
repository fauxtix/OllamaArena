# Como comparar modelos locais

**Guia de metodologia para tirar o melhor partido do laboratório de benchmarking** · Ollama Chat & Benchmark Laboratory · 10 de agosto de 2026

> **Fonte de verdade** em Markdown.

---

Objetivo deste guia: definir um fluxo repetível para responder à pergunta *"qual o melhor modelo para cada temática?"* (história, código, tradução, contos, anedotas...) e decidir quais os modelos que valem a pena manter carregados no Ollama para uso quase exclusivo no dia-a-dia.

## 1. O que a aplicação mede

A arquitetura assenta em dois eixos independentes:

- **Velocidade** (`BenchmarkResponse`): tokens por segundo, tempo de carga do modelo, tempo puro de geração e tamanho da resposta em tokens.
- **Qualidade** (`BenchmarkEvaluation`): 12 métricas — Factual, Formatação, Compliance, Relevância, Tom, Concisão, Clareza, Legibilidade, Halo Effect, Segurança, Consistência Idiomática e Detecção de Loop — mais uma nota global, avaliadas por dois juízes independentes (Gemini e OpenRouter).

Existe ainda um terceiro mecanismo-chave: o **ajuste automático de temperatura** (`ChatMeasureTemperature`). A aplicação classifica cada prompt e decide sozinha a temperatura: `0.10–0.30` para pedidos factuais (história, código, tradução) e `0.65–0.90` para criatividade (contos, anedotas, roleplay). Ou seja, a app já faz por nós a pergunta *"isto é factual ou criativo?"*.

### 1.1 As 12 métricas de qualidade (escala 1–5)

Cada juiz avalia a resposta em **12 critérios independentes** (1 = mau … 5 = excelente), mais a nota **Global**:

| # | Métrica | O que avalia |
|---|---|---|
| 1 | **Factual** | Veracidade, raciocínio lógico e profundidade das informações, considerando o conhecimento até ao ano de treino do modelo |
| 2 | **Formatação** | Correção de Markdown, estrutura, cumprimento de limites de palavras, resposta não truncada |
| 3 | **Compliance** | Cumprimento das restrições explícitas (positivas/negativas) do prompt |
| 4 | **Relevância** | Responde direta e exclusivamente à intenção do utilizador, sem tópicos paralelos |
| 5 | **Tom** | Adequação, profissionalismo e alinhamento do estilo com o esperado |
| 6 | **Concisão** | Eficiência de expressão, sem rodeios, repetições ou verborreia |
| 7 | **Clareza** | Facilidade de compreensão, fluxo lógico, ausência de ambiguidade |
| 8 | **Legibilidade** | Estrutura de leitura: frases, parágrafos, escaneabilidade visual |
| 9 | **Halo Effect** | Controlo de viés: uma nota não deve arrastar métricas independentes (peso 0 no score final) |
| 10 | **Segurança** | Guardrails: ausência de ódio, conteúdo perigoso ou conselhos prejudiciais |
| 11 | **Consistência idiomática** | Adesão estrita à língua do prompt, sem trocar de idioma a meio |
| 12 | **Detecção de Loop** | Saúde semântica: penaliza loops, frases repetidas e argumentos circulares |

A **Global** pondera as 12 métricas (a **Factual** tem o peso mais alto, 20) e a **recomendação** de cada juiz baseia-se no score factual.

## 2. A metodologia recomendada (5 passos)

### Passo 1 — Definir uma bateria de prompts por temática

O erro mais comum é comparar respostas a perguntas diferentes. Para que os modelos sejam comparáveis, cada um deve responder **às mesmas perguntas**. Construa 3–5 prompts por temática, formulados para disparar temperaturas distintas:

| Temática | Prompt exemplo (temperatura esperada) |
|---|---|
| Factual / história | "Explica o que causou a queda da República em Portugal em 1926" (~0.10–0.20) |
| Como-fazer / código | "Escreve uma função C# que calcula o desvio padrão" (~0.10–0.20, deteção de código) |
| Tradução | "Traduz para inglês: ..." (~0.24) |
| Criativo / anedota | "Conta-me uma anedota de programadores" (~0.78) |
| Criativo / conto | "Escreve um conto de 200 palavras sobre um gato astronauta" (~0.78–0.90) |

### Passo 2 — Carregar o conjunto candidato no Ollama

- Faça `ollama pull <modelo>` para os candidatos. Os modelos instalados aparecem automaticamente no seletor do chat; na página de Definições pode apenas escolher qual é o **modelo predefinido** (não é aí que se adicionam modelos).
- Inclua **a mesma família em tamanhos diferentes** (ex.: `qwen3:4b`, `qwen3:8b`, `qwen3:14b`) — a tese da aplicação é que um modelo pequeno pode superar um maior numa temática específica.

### Passo 3 — Executar a bateria

Para cada modelo, responda **às mesmas perguntas**, mudando apenas o modelo no seletor do chat. Cada resposta fica registada na base de dados com as métricas de velocidade, agrupada pela mesma pergunta (mesmo `PromptId`).

### Passo 4 — Avaliar cada resposta

- Use **"Avaliar automaticamente"** — submete o prompt de auditoria aos dois juízes (Gemini e OpenRouter) com contexto crítico para não penalizar o modelo por falta de conhecimento pós-treino.
- Se preferir, use **"Copiar prompt"** e cole o resultado manualmente.
- Avalie **todas** as respostas da bateria, não apenas uma.

### Passo 5 — Tirar conclusões em três locais

- **Benchmarks** (`/benchmark-evaluations`): tabela consolidada com filtros, export para Excel e análise local automática.
- **Qualidade e Métricas** (`/benchmarks`): comparação lado a lado por pergunta, com **gráficos por prompt** — tab **Desempenho** (tempos de execução/carga, velocidade e tokens, em barras) e tab **Qualidade** (**radares por juiz** com as 12 métricas na escala 0–5, que mostram o perfil completo de cada modelo de uma só vez) — tudo com download de imagem.
- **Análise automática** (`LocalAnalysisService`): identifica o mais rápido, o melhor avaliado, o consenso entre juízes e alerta se todos ficarem abaixo de 4.0/5.

## 3. Como ler os resultados

1. **Nunca olhe apenas para a nota final.** O valor está na separação **Score Factual vs Score Formatação**: um modelo pequeno pode ter `Factual=5, Formatação=2` (brilhante em lógica, fraco em markdown) e um grande o inverso. É esse o sinal para escolher por temática.
2. **Consenso entre juízes.** Se Gemini e OpenRouter divergem muito no `FactualRating`, desconfie do resultado isolado (a app sinaliza "divergência"). Se ambos concordam em 5, é um sinal forte.
3. **Cruzamento velocidade × qualidade.** Para uso quase exclusivo no dia-a-dia, o modelo mais rápido que mantenha qualidade ≥ 4 no *seu* tipo de prompt é melhor do que o melhor avaliado que demora 3× mais.
4. **Compatibilidade de VRAM** (badge GPU no chat): determina se o candidato corre na sua placa ou no CPU. Um modelo que "cabe na GPU" ganha sempre na prática.
5. **Alerta de qualidade baixa global.** Se *nenhum* modelo passa de 4/5 numa temática, a decisão correta é mudar de família ou de tamanho — não aceitar o menor dos maus.
6. **Factual baixo = sinal de alucinação.** Não há métrica dedicada a alucinações: respostas com afirmações inventadas tendem a ter `Factual` baixo. Mas os juízes avaliam apenas contra o **prompt + ano de treino**, sem verificação externa (não há pesquisa/ground truth) — contradições internas e factos claramente falsos são detetados, enquanto detalhes inventados mas plausíveis (citações, estatísticas, URLs) podem passar. Reforce com o consenso entre juízes (item 2): se ambos concordam em `Factual` baixo, é sinal forte.
7. **O radar vale mais do que a nota única.** No radar por juiz (tab Qualidade), um perfil "quadrado" e equilibrado (todas as métricas altas) é preferível a um com picos e vales; um perfil espetado aponta pontos fracos concretos (ex.: formatação ou raciocínio). O radar complementa a média ponderada do ranking — use-o para decidir entre modelos com pontuações finais próximas.

## 4. Mapa temática → perfil de modelo

| Uso pretendido | Métricas a pesar mais | Que modelo procurar |
|---|---|---|
| História / dúvidas factuais | Factual, Relevância, Velocidade | Pequeno–médio factual sólido (ex.: phi4-mini, qwen3 4–8b) |
| Código / instruções | Factual, Compliance, Formatação | Médio com forte cumprimento de instruções |
| Tradução | Factual, Clareza | Pequeno já chega (temperatura baixa) |
| Contos / anedotas / criativo | Tom, Criatividade, Formatação | Modelo maior ou focado em escrita (temperatura 0.65–0.90) |

**Resultado esperado:** 1 modelo "de uso geral" (vence nas temáticas que usa 80% do tempo) + 1 "especialista criativo" de reserva. Não precisa de manter 8 modelos carregados.

## 5. Notas e limitações atuais

- As correções associadas a este guia garantem que o agrupamento por pergunta (`PromptId`) funciona na grelha de Qualidade e Métricas, e que a análise local não rebenta quando ainda não existem avaliações factuais.
- A avaliação é **por resposta**, manual ou por juízes em nuvem (limitados a ~60 s entre chamadas para proteger os limites gratuitos).
- A **temperatura é automática**: para testar o mesmo modelo em modo criativo vs factual, a diferença tem de vir do próprio prompt — é esse o desenho da aplicação.
- O histórico de conversas do chat é **persistido na base de dados** (SQLite, tabelas `Conversas`/`ConversaMensagens`) e pode ser **exportado/importado em JSON** a partir do painel Histórico do Chat; as avaliações e os benchmarks também vivem na mesma base de dados.
