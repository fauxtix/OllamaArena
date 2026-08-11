# Como comparar modelos locais

**Guia de metodologia para tirar o melhor partido do laboratório de benchmarking** · Ollama Chat & Benchmark Laboratory · 10 de agosto de 2026

> **Fonte de verdade** em Markdown; o ficheiro `Docs/Guia_Como_Comparar_Modelos.pdf` é o artefacto de distribuição correspondente.

---

Objetivo deste guia: definir um fluxo repetível para responder à pergunta *"qual o melhor modelo para cada temática?"* (história, código, tradução, contos, anedotas...) e decidir quais os modelos que valem a pena manter carregados no Ollama para uso quase exclusivo no dia-a-dia.

## 1. O que a aplicação mede

A arquitetura assenta em dois eixos independentes:

- **Velocidade** (`BenchmarkResponse`): tokens por segundo, tempo de carga do modelo, tempo puro de geração e tamanho da resposta em tokens.
- **Qualidade** (`BenchmarkEvaluation`): 10 métricas — Factual, Formatação, Compliance, Relevância, Tom, Concisão, Clareza, Legibilidade, Halo Effect e Segurança — mais uma nota global, avaliadas por dois juízes independentes (Gemini e OpenRouter).

Existe ainda um terceiro mecanismo-chave: o **ajuste automático de temperatura** (`ChatMeasureTemperature`). A aplicação classifica cada prompt e decide sozinha a temperatura: `0.10–0.30` para pedidos factuais (história, código, tradução) e `0.65–0.90` para criatividade (contos, anedotas, roleplay). Ou seja, a app já faz por nós a pergunta *"isto é factual ou criativo?"*.

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

- Faça `ollama pull <modelo>` para os candidatos e adicione-os na página de Definições para aparecerem no seletor do chat.
- Inclua **a mesma família em tamanhos diferentes** (ex.: `qwen3:4b`, `qwen3:8b`, `qwen3:14b`) — a tese da aplicação é que um modelo pequeno pode superar um maior numa temática específica.

### Passo 3 — Executar a bateria

Para cada modelo, responda **às mesmas perguntas**, mudando apenas o modelo no seletor do chat. Cada resposta fica registada na base de dados com as métricas de velocidade, agrupada pela mesma pergunta (mesmo `PromptId`).

### Passo 4 — Avaliar cada resposta

- Use **"Avaliar automaticamente"** — submete o prompt de auditoria aos dois juízes (Gemini e OpenRouter) com contexto crítico para não penalizar o modelo por falta de conhecimento pós-treino.
- Se preferir, use **"Copiar prompt"** e cole o resultado manualmente.
- Avalie **todas** as respostas da bateria, não apenas uma.

### Passo 5 — Tirar conclusões em três locais

- **Qualidade e Métricas** (`/benchmark-evaluations`): grelha com filtros e análise local automática.
- **Benchmarks** (`/benchmarks`): comparação lado a lado por pergunta.
- **Análise automática** (`LocalAnalysisService`): identifica o mais rápido, o melhor avaliado, o consenso entre juízes e alerta se todos ficarem abaixo de 4.0/5.

## 3. Como ler os resultados

1. **Nunca olhe apenas para a nota final.** O valor está na separação **Score Factual vs Score Formatação**: um modelo pequeno pode ter `Factual=5, Formatação=2` (brilhante em lógica, fraco em markdown) e um grande o inverso. É esse o sinal para escolher por temática.
2. **Consenso entre juízes.** Se Gemini e OpenRouter divergem muito no `FactualRating`, desconfie do resultado isolado (a app sinaliza "divergência"). Se ambos concordam em 5, é um sinal forte.
3. **Cruzamento velocidade × qualidade.** Para uso quase exclusivo no dia-a-dia, o modelo mais rápido que mantenha qualidade ≥ 4 no *seu* tipo de prompt é melhor do que o melhor avaliado que demora 3× mais.
4. **Compatibilidade de VRAM** (badge GPU no chat): determina se o candidato corre na sua placa ou no CPU. Um modelo que "cabe na GPU" ganha sempre na prática.
5. **Alerta de qualidade baixa global.** Se *nenhum* modelo passa de 4/5 numa temática, a decisão correta é mudar de família ou de tamanho — não aceitar o menor dos maus.

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
- O histórico de conversas do chat vive no navegador (localStorage); apenas os dados de benchmark e as avaliações são persistidos na base de dados.
