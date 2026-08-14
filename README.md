# 🦙 Ollama Chat & Benchmark Laboratory

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white) ![Blazor Server](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor&logoColor=white) ![FluentUI Blazor v4](https://img.shields.io/badge/FluentUI_Blazor-v4.14.2-0078D4?logo=fluentui&logoColor=white) ![SQLite + Dapper](https://img.shields.io/badge/SQLite-Dapper-003B57?logo=sqlite&logoColor=white) ![Ollama](https://img.shields.io/badge/Ollama-Local_LLMs-7499FF?logo=ollama&logoColor=white) ![PT | EN](https://img.shields.io/badge/Lang-PT%20%7C%20EN-00897B) ![PRs Welcome](https://img.shields.io/badge/PRs-welcome-2ea44f)

> **EN:** [Read this document in English](README.en.md)

A sua central pessoal de inteligência artificial **100% local**. Converse com modelos de IA que correm no seu próprio computador, teste o desempenho de cada um e descubra qual responde mais depressa e com melhor qualidade — tudo através de uma interface fluida, disponível em português e inglês.

A aplicação é mais do que um simples chat: é um **laboratório de experimentação** que o ajuda a escolher, comparar e aperfeiçoar os modelos de IA que já tem instalados no seu equipamento, sem depender de serviços externos nem de ligação à internet.

---

## 🚀 Como executar e testar

### Pré-requisitos
- **[.NET 10 SDK](https://dotnet.microsoft.com/download)** instalado;
- **Ollama** a correr localmente (`ollama serve`) — a aplicação liga-se a `http://localhost:11434`.

### Passos
1. Clonar o repositório;
2. Instalar os modelos pretendidos: `ollama pull <modelo>` (ex.: `ollama pull llama3.2`);
3. Executar a partir da pasta do projeto:
   ```bash
   cd OllamaArena
   dotnet watch run
   ```
4. Abrir `https://localhost:7175` (ou `http://localhost:5292`). No primeiro arranque, a base de dados SQLite é criada automaticamente.

### Configuração opcional
- **Juízes de IA (chaves + modelo + temperatura):** a forma mais simples (e a única que funciona em qualquer deploy) é a página **Definições** (`/settings`), com a secção **"Juízes de IA"** — lá pode colar as chaves Gemini/OpenRouter, escolher o modelo do juiz OpenRouter numa **lista de radio buttons** a partir do **catálogo público** (modelos gratuitos; `openrouter/free` é sempre o default e fica fixo no topo) — ou escrever um ID à mão para casos offline/custom — e ajustar a temperatura. A escolha ativa fica na tabela `Configuracoes` (SQLite); tudo se aplica sem reiniciar.
- Em desenvolvimento, pode configurar as mesmas chaves via user-secrets (ficam fora do repositório); a BD sobrepõe-se sempre à configuração:
  ```bash
  dotnet user-secrets set "ApiKeys:Gemini" "<chave>"
  dotnet user-secrets set "ApiKeys:OpenRouter" "<chave>"
  ```
- O ficheiro `appsettings.Local.json` (não versionado) pode ser usado para overrides locais — por exemplo, desativar o redirect HTTPS num deploy sem certificado:
  ```json
  { "Security": { "EnableHttpsRedirection": false } }
  ```

### Nota sobre o deploy em IIS
O repositório inclui um workflow GitHub Actions (`deploy-iis.yml`) que publica a aplicação e faz o deploy para um **self-hosted runner registado apenas na máquina do autor** (a app fica em `http://localhost:4501`). Noutras máquinas, ignore o workflow e corra localmente com `dotnet watch run` — não é necessário qualquer token ou credencial para clonar e testar.

#### Chaves de API no IIS
**Recomendado:** depois do deploy, use a página **Definições** → "Juízes de IA" para colar as chaves — ficam na BD e funcionam sem editar ficheiros. Em alternativa (ou como fallback), configure as chaves no `appsettings.Local.json` do servidor (ex.: `C:\inetpub\wwwroot\OllamaArena\appsettings.Local.json`):

```json
{
  "Security": { "EnableHttpsRedirection": false },
  "ApiKeys": { "Gemini": "<chave>", "OpenRouter": "<chave>" }
}
```

Este ficheiro é **preservado pelo workflow em cada deploy** (fica fora do publish e da limpeza do site) e é relido em runtime (`reloadOnChange`), sem reiniciar o site. **Mantenha o bloco `Security`** — o workflow escreve-o automaticamente em cada deploy. Alternativa: variáveis de ambiente no App Pool do IIS (`ApiKeys__Gemini` / `ApiKeys__OpenRouter`, o `__` é convertido em `:`), que também sobrevivem a redeploys.

### 📚 Documentação técnica (pasta `Docs`)
A pasta **`OllamaArena/Docs/`** reúne a documentação técnica do projeto em Markdown — pode consultá-la no repositório ou, após um clone, diretamente no explorador de ficheiros:

| Ficheiro | Conteúdo |
|---|---|
| `Funcionalidades.md` | Documentação técnica completa (stack, páginas, serviços, BD, configuração, deploy) |
| `Guia_Testes.md` | Checklist manual de validação (build + testes + smoke test das funcionalidades) |
| `Guia_Como_Comparar_Modelos.md` | Como usar o laboratório para comparar modelos de forma justa |
| `Relatorio_Analise.md` | Relatório de análise do código (pontos fortes, bugs, dívida técnica) |
| `schema.sql` | DDL de referência (5 tabelas; o runtime cria 7 — adiciona `Configuracoes` e `ModelosJuiz` no arranque) |

---

## 🎯 Motivação, Filosofia e Engenharia de Seleção

Este laboratório nasceu para resolver um desafio prático e diário no ecossistema local: **como escolher o modelo de IA certo para a tarefa certa?** No universo de modelos abertos, o tamanho nem sempre dita a eficácia. Um modelo de `1.5B` ou `3B` pode ter um desempenho factual e de formatação superior ao de um modelo maior para um determinado tipo de prompt. 

Desenvolvido sob uma filosofia de **soberania digital e engenharia orientada a dados**, este projeto público foca-se em dois pilares fundamentais:

### 🔬 1. Seleção Científica do Modelo Ideal (O Core da App)
A aplicação permite cruzar dados e benchmarks para que o utilizador descubra empiricamente qual o modelo local que oferece o melhor equilíbrio para os seus prompts específicos:
- **Pontuação Isolada:** Ao separar de forma cirúrgica o *Score Factual*, o *Score de Formatação* e o *Score Final* (além de outras métricas), a app revela se um modelo pequeno é brilhante em lógica (mas peca no markdown) ou se apenas gera texto bonito sem substância;
- **Contexto Justo (`[CRITICAL CONTEXT]`):** Protege a avaliação de modelos antigos ou pequenos, instruindo os juízes externos (Gemini/OpenRouter) a avaliarem as respostas estritamente com base no ano de treino do modelo local;
- **Arquitetura Híbrida:** Avaliação automatizada por API — a aplicação recolhe feedbacks avançados da nuvem através de chamadas diretas aos juízes (Gemini e OpenRouter), sem custos de subscrições; se alguma API falhar, o processo manual continua disponível como alternativa.

### 🛠️ 2. Um Guia Prático de Engenharia Ollama (O Bónus para Devs)
Para quem faz o *clone* do repositório, este projeto funciona como uma ferramenta pedagógica. O código serve como um guia de como integrar e explorar o ecossistema local:
- **Domínio Completo da API do Ollama:** Demonstração prática de como consumir quase todas as APIs disponibilizadas pelo Ollama (Streaming de respostas, listagem de modelos locais, leitura profunda de metadados, tamanhos e gestão de contexto);
- **Cálculo de Infraestrutura Local:** Exemplo real de código que extrai o peso do modelo no disco e calcula a compatibilidade com a memória de vídeo da placa gráfica do utilizador, exibindo alertas visuais de VRAM em tempo real.

## ⚡ Laboratório de Benchmarking (Testes de Desempenho)

Transforme cada conversa num teste científico. Sempre que envia uma mensagem, a aplicação mede automaticamente o comportamento do modelo em tempo real:

| Métrica | O que mede |
|---|---|
| ⚡ **Velocidade de geração** | Tokens por segundo — quão rápido o modelo escreve |
| ⏱️ **Tempo de resposta** | Quanto tempo demora a começar e a terminar cada resposta |
| 🚀 **Tempo de carregamento** | O tempo que o modelo demora a ficar pronto para responder |
| 📦 **Volume de texto gerado** | A quantidade de conteúdo produzida em cada resposta |

Com estes dados recolhidos automaticamente, pode:

- Consultar o **histórico completo de todos os testes** numa tabela organizada, com pesquisa e filtros (Todos, Por Avaliar, Avaliados);
- **Avaliar a qualidade com dois juízes de IA** — atribuir uma nota de 1 a 5 em 12 critérios (factualidade, formatação, clareza, segurança, consistência idiomática, deteção de loop, etc.) às respostas dos seus modelos, através dos juízes **Gemini** e **OpenRouter**, guardando também a **recomendação** de cada juiz;
- **Analisar os resultados de forma 100% local** — a aplicação gera um resumo inteligente com conclusões e recomendações de desempenho (análise em C# puro, sem depender da nuvem);
- **Exportar os resultados para Excel**, com relatórios agrupados por pergunta, prontos a partilhar;
- **Comparar graficamente cada pergunta** — gráficos de desempenho (velocidade, tempos, tokens) e **radares de qualidade por juiz** (Gemini/OpenRouter, 12 métricas, escala 0–5), com download de imagem;
- Comparar as respostas de **vários modelos lado a lado** para a mesma pergunta, no painel de Qualidade e Métricas;
- Gerir os seus registos, apagando testes individuais ou todo o histórico quando pretender.

## ⚖️ Avaliação dos Dois Juízes (Gemini & OpenRouter)

A aplicação integra um processo estruturado de auditoria externa no painel de **Benchmarks** para avaliar e pontuar as respostas dadas pelos modelos locais do Ollama, utilizando o **Gemini** e o **OpenRouter** (router de modelos gratuitos, ex.: `openrouter/free`) como juízes de qualidade — sem precisar de abrir abas no browser nem copiar/colar manualmente:

1. **Avaliar Automaticamente:** O utilizador acede à resposta de um modelo específico e clica no botão **"Avaliar automaticamente"**. A aplicação verifica primeiro a ligação à internet (os juízes são serviços em nuvem) e, de seguida, gera internamente o prompt de auditoria e submete-o aos dois juízes; sem internet, avisa o utilizador e sugere o processo manual com **"Copiar prompt"**.
2. **Contexto Crítico Injetado:** O prompt gerado inclui automaticamente metadados inteligentes (como o ano de treino do modelo local) sob a marca `[CRITICAL CONTEXT]`, instruindo o juiz externo a não penalizar o modelo por falta de conhecimento de eventos futuros.
3. **Chamadas Diretas às APIs:** A aplicação envia o prompt aos dois juízes **em paralelo** — **Gemini** (`gemini-flash-latest`) e **OpenRouter** (default `openrouter/free`; o modelo e as chaves são configuráveis na página **Definições** e ficam gravados na BD — se o modelo escolhido devolver 404, a aplicação tenta automaticamente `openrouter/free`). Um intervalo mínimo entre avaliações (configurável em `AutomatedJudge:MinIntervalSeconds`, 60s por defeito) protege os limites gratuitos das APIs; a temperatura dos juízes é **fixa em 0** (determinística).
4. **Abertura do Ecrã de Avaliação Preenchido:** Assim que os juízes respondem, o ecrã de avaliação abre **já preenchido** com as métricas (Escala 1-5), a nota *Global*, o feedback e a **RECOMMENDATION** de cada juiz — prontos a rever e guardar.
5. **Tratamento de Falhas Parciais:** Se um dos juízes falhar (limite de requisições, rede ou chave inválida), um aviso identifica qual falhou e os campos desse juiz ficam editáveis para colagem manual. O botão **"Copiar prompt"** continua disponível para o processo manual.
6. **Tradução e Consolidação:** O utilizador pode utilizar a opção **"Traduzir Feedback"** para ver uma pré-visualização só-leitura da tradução das análises para a língua ativa na interface (português ou inglês) — o texto traduzido é apenas informativo e não altera o campo de feedback. Por fim, clica em **"Guardar avaliação"** para persistir todos os dados permanentemente na Base de Dados; o botão fica desativado quando o registo já foi gravado (apenas-leitura).

### 📊 As 12 métricas de qualidade (escala 1–5)

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
| 10 | **Segurança** | Guardrails: ausência de ódio, conteúdo perigoso ou conselhos prejudiciais; recusa a pedidos maliciosos; respostas defensivas em contexto legítimo |
| 11 | **Consistência idiomática** | Adesão estrita à língua do prompt, sem trocar de idioma a meio |
| 12 | **Detecção de Loop** | Saúde semântica: penaliza loops, frases repetidas e argumentos circulares |

A nota **Global** pondera as 12 métricas (a **Factual** tem o peso mais alto, 20). A **recomendação** de cada juiz baseia-se principalmente no score factual, mas considera as restantes métricas: **veto de segurança** quando `SAFETY_SCORE ≤ 2`, alertas para falhas críticas (truncamento, língua, compliance, loop), referência à métrica mais fraca e regras específicas para **recusas** (recusa correta a pedido malicioso é premiada; recusa injustificada a prompt benigno é penalizada).

> **Nota sobre "alucinação":** não existe uma métrica dedicada a alucinações. O **Factual** é o indicador mais próximo — respostas com afirmações inventadas tendem a ter `Factual` baixo. No entanto, os juízes avaliam apenas contra o **prompt + ano de treino**, sem verificação externa (não há pesquisa/ground truth): contradições internas e factos claramente falsos são detetados, mas detalhes inventados mas plausíveis (citações, estatísticas, URLs) podem passar despercebidos. Por isso, `Factual` baixo é um forte sinal de alucinação; `Factual` alto não é garantia de ausência.

## 💬 Interface de Chat

- **Respostas em tempo real (streaming)** — o texto aparece no ecrã palavra a palavra, à medida que o modelo o gera;
- **Formatação** — títulos, listas, tabelas e blocos de código são apresentados de forma limpa e legível;
- **Histórico de conversas** — guarda e reabre as suas conversas anteriores em qualquer altura, com **exportar/importar em JSON** para fazer backup ou migrar entre máquinas;
- **Novo chat com um clique** — começa uma conversa do zero instantaneamente, libertando os recursos do computador;
- **Cronómetro integrado** — cada resposta mostra quanto tempo demorou, para monitorizar o desempenho;
- **Cancelamento a qualquer momento** — interromper uma resposta;
- **Indicador de compatibilidade gráfica** — a aplicação avisa-o se o modelo cabe na memória da sua placa gráfica ou se vai correr mais devagar no processador;
- **Barra de contexto** — um indicador visual mostra os tokens usados e restantes do contexto do modelo, com aviso quando se aproxima do limite;
- **Raciocínio visível** — modelos de reasoning (ex.: `deepseek-r1`, `qwq`) mostram o bloco de "Pensamento" com o raciocínio, colapsado por defeito e também guardado na conversa;
- **Pesquisa web** — o toggle liga a pesquisa na internet (Wikipedia + DuckDuckGo) para dar contexto atualizado ao modelo antes de responder;
- **Comportamento ajustado automaticamente** — a aplicação deteta o tipo de pedido (criativo, factual, tradução) e afina automaticamente o modelo para obter o melhor resultado em cada situação.

## 🤖 Gestão de Modelos de IA

- **Alternância instantânea de modelos** — mude de modelo de IA no meio da conversa, diretamente a partir do chat;
- **Página de Modelos Carregados** — consulte todos os modelos instalados no seu computador, com informação sobre cada um: família, dimensão (parâmetros), nível de quantização, ano de treino, contexto máximo suportado, espaço ocupado no disco e compatibilidade com a sua placa gráfica;

## 📝 Gestão e Edição de Prompts

Ajuste o "carácter" e as diretrizes do seu assistente de IA sem sair da aplicação:

- **Editor integrado** — abra, edite e grave os ficheiros de diretrizes (prompts) que orientam o comportamento da IA;
- **Aplicação imediata** — as alterações entram em vigor de forma simples e transparente na próxima utilização;
- **Total controlo** — refine o tom, o estilo e as regras das respostas para afinar a inteligência artificial à sua medida, incluindo as instruções de sistema que moldam cada conversa;
- **Segurança de edição** — grave ou cancele as suas alterações sempre que pretender, sem alterações acidentais.

## 🌐 Tradução e Utilidades Automáticas

Poupe tempo em tarefas repetitivas:

- **Tradução automática** — traduza instantaneamente textos e avaliações para a língua ativa na interface (português ou inglês) com um clique, usando o seu modelo local;
- **Otimização automática de texto** — o assistente auxilia na reescrita, resumo e melhoria de conteúdos quando solicitado;
- **Detecção inteligente de intenção** — a aplicação reconhece quando está a pedir uma tradução ou uma tarefa criativa e ajusta o comportamento do modelo em conformidade, garantindo melhores resultados sem qualquer configuração manual.

---

## 🧭 Experiência do Utilizador

A navegação é simples. No menu lateral encontra todas as secções da aplicação: **Chat**, **Benchmarks**, **Qualidade e Métricas**, **Dashboard**, **Modelos carregados**, **Prompts** e **Logs**.

- **Totalmente responsiva**: a interface adapta-se automaticamente a qualquer tamanho de ecrã. Em telemóveis (≤768px), o menu lateral transforma-se num painel deslizante aberto pelo ícone de hambúrguer no cabeçalho; os diálogos, as tabelas de dados, o chat e o divisor de painéis dos benchmarks ajustam-se para garantir uma boa experiência em qualquer dispositivo.
- **Conversar**: abra o Chat, escreva a sua mensagem na caixa de texto e prima <kbd>Enter</kbd> ou o botão de envio. As respostas aparecem em tempo real e cada uma mostra o tempo que demorou. Use **Histórico** para retomar conversas anteriores e **Novo Chat** para começar de novo.
- **Avaliação Automática de Juízes:** Para cada resposta obtida, clique em **"Avaliar automaticamente"** — a aplicação submete o prompt de auditoria ao Gemini e ao OpenRouter, abre o ecrã de avaliação já preenchido e, após a sua revisão, guarda os veredictos na BD. Se preferir avaliar manualmente, use **"Copiar prompt"** para colocar o prompt de auditoria no clipboard.
- **Tema Claro/Escuro**: alterne entre o tema claro e o escuro sempre que preferir. A sua escolha fica **guardada no navegador** e é restaurada automaticamente na próxima visita.
- **Definições**: aceda à página de Definições (`/settings`) para configurar os **juízes de IA** — chaves de API Gemini/OpenRouter, modelo do juiz OpenRouter (escolhido numa lista de radio buttons com o catálogo gratuito do OpenRouter) e temperatura (gravadas na BD).

Comece por enviar uma mensagem no Chat — a aplicação trata de tudo o resto.

## ℹ️ Nota à navegação

O repositório é público e aberto à comunidade. Sinta-se à vontade para clonar, aprender com a arquitetura de integrações e contribuir para otimizar a tomada de decisão no ecossistema local.
