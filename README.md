# 🦙 Ollama Chat & Benchmark Laboratory

> **EN:** [Read this document in English](README.en.md)

Central pessoal de inteligência artificial **100% local**. Converse com modelos de IA que correm no seu próprio computador, teste o desempenho de cada um e descubra qual é que responde mais depressa e com melhor qualidade — tudo através de uma interface disponível em português e inglês.

A aplicação é mais do que um simples chat: é um **laboratório de experimentação** que o ajuda a escolher, comparar e aperfeiçoar os modelos de IA que já tem instalados no seu equipamento. O chat, os benchmarks, a base de dados e a análise de resultados correm **100% no seu computador**, sem serviço externo.

> **Onde é preciso internet** (tudo o resto funciona offline): os **juízes de IA na nuvem** — Gemini e OpenRouter — e a avaliação automática que os usa; os **gráficos e radares**, porque a biblioteca Chart.js é carregada a partir de um CDN.

---

## 🎯 Motivação, Filosofia e Engenharia de Seleção

Este laboratório nasceu para resolver um desafio prático e diário no ecossistema local: **como escolher o modelo de IA certo para a tarefa certa?** No universo de modelos abertos, o tamanho nem sempre dita a eficácia. Um modelo de `1.5B` ou `3B` pode ter um desempenho factual e de formatação superior ao de um modelo maior para um determinado tipo de prompt. 

Desenvolvido sob uma filosofia de **soberania digital e engenharia orientada a dados**, este projeto público foca-se em dois pilares fundamentais:

### 🔬 1. Seleção Científica do Modelo Ideal (O Core da App)
A aplicação permite cruzar dados e benchmarks para que o utilizador descubra empiricamente qual o modelo local que oferece o melhor equilíbrio para os seus prompts específicos:
- **Pontuação Isolada:** Ao separar de forma cirúrgica o *Score Factual*, o *Score de Formatação* e o *Score Final* (além de outras métricas), a app revela se um modelo pequeno é brilhante em lógica (mas peca no markdown) ou se apenas gera texto bonito sem substância;
- **Contexto Justo (`[CRITICAL CONTEXT]`):** Protege a avaliação de modelos antigos ou pequenos, instruindo os juízes externos (Gemini/OpenRouter) a avaliarem as respostas estritamente com base no ano de treino do modelo local;
- **Arquitetura Híbrida:** Avaliação automatizada por API — a aplicação recolhe feedbacks avançados da nuvem através de chamadas diretas aos juízes (Gemini/OpenRouter), sem custos de subscrições; se alguma API falhar, o processo manual continua disponível como alternativa.

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

Os testes vivem em **duas páginas** que se complementam:

- **Benchmarks** (`/benchmark-evaluations`) — o histórico completo de todos os testes: tabela com pesquisa e filtros, exportações, análise local e eliminação de registos;
- **Qualidade e Métricas** (`/benchmarks`) — a vista de cada pergunta: as respostas de todos os modelos, os gráficos e a avaliação pelos dois juízes.

Com estes dados recolhidos automaticamente, pode:

- Consultar o **histórico completo de todos os testes** numa tabela organizada, com pesquisa e filtros (Todos, Por Avaliar, Avaliados) — em **Benchmarks**;
- **Avaliar a qualidade com dois juízes de IA** — atribuir uma nota de 1 a 5 em 12 critérios (factualidade, formatação, clareza, segurança, consistência idiomática, deteção de loop, etc.) às respostas dos seus modelos, através dos juízes externos (**Gemini** e **OpenRouter**), guardando também a **recomendação** de cada juiz — em **Qualidade e Métricas**, com o botão **"Avaliar automaticamente"**;
- **Analisar os resultados de forma 100% local** — a aplicação gera um resumo com conclusões e recomendações de desempenho (análise em C# puro, sem depender da nuvem) — em **Benchmarks**, no diálogo de análise com o modelo local;
- **Exportar os resultados para Excel**, com relatórios agrupados por pergunta, prontos a partilhar — em **Benchmarks**;
- **Exportar para PDF** com relatórios detalhados por modelo (métricas, feedback e recomendações de cada juiz) — em **Benchmarks**; o botão só fica disponível quando não há nenhum teste por avaliar;
- **Comparar graficamente cada pergunta** — gráficos de desempenho (velocidade, tempos, tokens) e **radares de qualidade por juiz** (Gemini/OpenRouter, 12 métricas, escala 0–5), com download de imagem — em **Qualidade e Métricas**;
- Ver as **respostas de todos os modelos da mesma pergunta**, empilhadas na mesma página, e abrir o **detalhe de uma resposta** com as notas por critério, o feedback e a recomendação de cada juiz — em **Qualidade e Métricas**;
- Gerir os seus registos com **eliminação seletiva** — apagar testes individuais, apenas os **por avaliar**/**avaliados**, os **filtrados/pesquisados**, ou todo o histórico, em **Benchmarks**; em **Qualidade e Métricas** pode ainda apagar a pergunta inteira com todas as respostas.

> **Nota:** Os ficheiros exportados (Excel, PDF e imagens de gráficos/radares) são descarregados automaticamente para a **pasta de Downloads** do navegador.

## ⚖️ Avaliação dos Dois Juízes (Gemini & OpenRouter)

A aplicação integra um processo estruturado de auditoria externa na página **Qualidade e Métricas** (`/benchmarks`) para avaliar e pontuar as respostas dadas pelos modelos locais do Ollama, utilizando o **Gemini** e o **OpenRouter** (router de modelos gratuitos, ex.: `openrouter/free`) como juízes de qualidade — sem precisar de abrir abas no browser nem copiar/colar manualmente:

1. **Avaliar Automaticamente:** O utilizador acede à resposta de um modelo específico e clica no botão **"Avaliar automaticamente"**. A aplicação verifica primeiro a ligação à internet (os juízes são serviços em nuvem) e, de seguida, gera internamente o prompt de auditoria e submete-o aos dois juízes; se um dos juízes falhar (ou ambos), avisa o utilizador e sugere o processo manual com **"Copiar prompt"**.
2. **Contexto Crítico Injetado:** O prompt gerado inclui automaticamente metadados (como o ano de treino do modelo local) sob a marca `[CRITICAL CONTEXT]`, instruindo o juiz externo a não penalizar o modelo por falta de conhecimento de eventos futuros. Juntam-se dois blocos calculados pela aplicação: `[EXPECTED RESPONSE LANGUAGE]`, com a língua em que a resposta foi pedida — uma resposta noutra língua obriga o juiz a dar no máximo 2 à **Consistência idiomática**; e o **tema detetado do prompt** (factual, criativo, conversacional, código ou sensível à segurança), que adapta os pesos da recomendação — nos temas criativos e conversacionais o **Tom** e a **Relevância** pesam tanto como o Factual, e nos temas sensíveis à segurança o veto de Segurança prevalece.
3. **Chamadas Diretas às APIs:** A aplicação envia o prompt aos dois juízes **em paralelo** — **Gemini** (`gemini-flash-latest`) e **OpenRouter** (default `openrouter/free`; o modelo e as chaves são configuráveis na página **Definições** e ficam gravados na BD — se o modelo escolhido devolver 404, a aplicação tenta automaticamente `openrouter/free`). Um intervalo mínimo entre avaliações (configurável em `AutomatedJudge:MinIntervalSeconds`, 60s por defeito) protege os limites gratuitos das APIs; a temperatura dos juízes é **fixa em 0** (determinística).
4. **Abertura do Ecrã de Avaliação Preenchido:** Assim que os juízes respondem, o ecrã de avaliação abre **já preenchido** com as métricas (Escala 1-5), a nota *Global*, o feedback e a **RECOMMENDATION** de cada juiz — prontos a rever e guardar. Os rótulos dos dois juízes indicam a **origem real** de cada avaliação (automática, externa colada manualmente, ou "sem resposta"), que fica persistida na BD.
5. **Tratamento de Falhas Parciais:** Se um dos juízes (ou ambos) não conseguir responder — ex.: `Too many requests`, demasiado tráfego, rede ou chave inválida — um aviso identifica qual(ais) falhou(ram) e os campos desse(s) juiz(es) ficam editáveis para colagem manual. O utilizador tem **três opções**: (1) **aguardar** e tentar a avaliação automática mais tarde; (2) **aproveitar a resposta do juiz que respondeu** e preencher manualmente apenas os campos do outro; ou (3) **fazer toda a avaliação manualmente** — o botão **"Copiar prompt"** continua disponível para obter o prompt de avaliação sem precisar de internet.
6. **Tradução e Consolidação:** O utilizador pode traduzir o feedback de cada juiz com **"Traduzir Feedback"** (um botão por juiz: Gemini e OpenRouter) para ver uma pré-visualização só-leitura da tradução das análises para a língua ativa na interface (português ou inglês) — o texto traduzido é apenas informativo e não altera o campo de feedback. Por fim, clica em **"Guardar avaliação"** para persistir todos os dados permanentemente na Base de Dados; o botão fica desativado quando o registo já foi gravado (apenas-leitura).

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

A nota **Global** pondera as 12 métricas (a **Factual** tem o peso mais alto, 20; o **Halo Effect** tem peso 0). A **recomendação** de cada juiz baseia-se principalmente no score factual, mas considera as restantes métricas: **veto de segurança** quando `SAFETY_SCORE ≤ 2`, alertas para falhas críticas (truncamento, língua, compliance, loop), referência à métrica mais fraca e regras específicas para **recusas** (recusa correta a pedido malicioso é premiada — e o juiz tem de assinalar `REFUSAL_HANDLED: yes` —; recusa injustificada a prompt benigno é penalizada).

> **Nota sobre "alucinação":** não existe uma métrica dedicada a alucinações. O **Factual** é o indicador mais próximo — respostas com afirmações inventadas tendem a ter `Factual` baixo. No entanto, os juízes avaliam apenas contra o **prompt + ano de treino**, sem verificação externa (não há pesquisa/ground truth): contradições internas e factos claramente falsos são detetados, mas detalhes inventados mas plausíveis (citações, estatísticas, URLs) podem passar despercebidos. Por isso, `Factual` baixo é um forte sinal de alucinação; `Factual` alto não é garantia de ausência.

## 📈 Dashboard

O painel **Dashboard** consolida tudo o que os juízes avaliaram num único ecrã:

- **6 cartões de resumo** — Melhor Modelo (com score), Modelos avaliados, Cobertura de avaliação (% de respostas com os dois juízes), Prompts testados (com a data do último benchmark), Modelo mais rápido (tokens/s) e Maior divergência entre juízes (o modelo sobre o qual Gemini e OpenRouter mais discordam);
- **2 radares de qualidade** — um por juiz (Gemini/OpenRouter), com a média das 12 métricas por modelo em escala 0–5 e download de imagem;
- **Desempenho vs Qualidade** — gráfico de dispersão tokens/s vs score global, para ver de relance que modelos são rápidos *e* bons;
- **Evolução do score ao longo do tempo** — gráfico de linhas com a média acumulada do score de cada modelo por data de benchmark, para ver tendências e declínios;
- **Tabela classificativa ordenável** — posição, modelo, score global (barra 0–5), notas separadas de Gemini e OpenRouter, Tokens/s médios, Tempo médio de resposta, Divergência entre juízes ("—" quando só um juiz avaliou) e Tokens médios por resposta.

## 💬 Interface de Chat

- **Respostas em tempo real (streaming)** — o texto aparece no ecrã palavra a palavra, à medida que o modelo o gera;
- **Formatação** — títulos, listas, tabelas e blocos de código são apresentados de forma limpa e legível;
- **Guardar automático** — cada conversa é gravada automaticamente na base de dados após a resposta, sem diálogos de confirmação; o modelo gera um resumo curto (SUMMARY) para descrever o prompt, com fallback inteligente para frases significativas;
- **Histórico de conversas** — guarda e reabre as suas conversas anteriores em qualquer altura, com **exportar/importar em JSON** para fazer backup ou migrar entre máquinas; o painel lateral tem dois separadores, **Conversas** e **Benchmarks** (a lista de perguntas já testadas);
- **Novo chat com um clique** — começa uma conversa do zero instantaneamente, libertando os recursos do computador; é também a forma de mudar de modelo durante os testes, porque cada conversa fica associada a um único modelo (ver "Gestão de Modelos de IA");
- **Cronómetro integrado** — cada resposta mostra quanto tempo demorou, para monitorizar o desempenho;
- **Cancelamento a qualquer momento** — interromper uma resposta;
- **Indicador de compatibilidade gráfica** — a aplicação avisa-o se o modelo cabe na memória da sua placa gráfica ou se vai correr mais devagar no processador;
- **Barra de contexto** — um indicador visual mostra os tokens usados e restantes do contexto do modelo, com aviso quando se aproxima do limite;
- **Raciocínio (think)** — modelos com capacidade de raciocínio (ex.: `deepseek-r1`, `qwq`) mostram o bloco de "Pensamento" com o raciocínio, colapsado por defeito e também guardado na conversa. **Por defeito está desligado**: active o toggle **"Ativar reasoning (think)"** em **Definições** (`/settings`, gravado na BD como `Chat:EnableReasoning`); enquanto estiver desligado o raciocínio não é capturado, guardado nem mostrado;
- **Comportamento ajustado automaticamente** — a aplicação deteta o tipo de pedido e afina automaticamente os parâmetros do modelo para obter o melhor resultado em cada situação: os pedidos **criativos** (escrever, imaginar, role play) recebem uma temperatura mais alta e os **factuais** (explicar, calcular, comparar, traduzir) uma mais baixa, de forma determinística.

## 🤖 Gestão de Modelos de IA

- **Um modelo por conversa** — cada conversa corre num único modelo, escolhido no início; para comparar a resposta ao mesmo pedido em modelos diferentes, clique **Novo chat** e selecione outro modelo. O histórico mantém cada conversa associada ao seu modelo, o que torna a comparação fiável;
- **Página de Modelos Carregados** — consulte todos os modelos instalados no seu computador, com informação sobre cada um: família, dimensão (parâmetros), nível de quantização, ano de treino, contexto máximo suportado, espaço ocupado no disco, **badge de raciocínio** (suporta / não suporta / desconhecido) e compatibilidade com a sua placa gráfica. O botão **"O que significa cada coluna?"** explica cada coluna em detalhe;

## 📝 Gestão e Edição de Prompts

Ajuste o "carácter" e as diretrizes do seu assistente de IA sem sair da aplicação:

- **Editor integrado** — abra, edite e grave os ficheiros de diretrizes (prompts) que orientam o comportamento da IA;
- **Aplicação imediata** — as alterações entram em vigor de forma simples e transparente na próxima utilização;
- **Total controlo** — refine o tom, o estilo e as regras das respostas para afinar a inteligência artificial à sua medida, incluindo as instruções de sistema que moldam cada conversa;
- **Segurança de edição** — grave ou cancele as suas alterações sempre que pretender, sem alterações acidentais.

## 🌐 Tradução e Utilidades Automáticas

Poupe tempo em tarefas repetitivas:

- **Tradução automática** — traduza instantaneamente os feedbacks dos juízes para a língua ativa na interface (português ou inglês) com um clique, através de uma cadeia de sistemas por ordem de preferência **OpenRouter → Gemini → Ollama (local)**. Se o texto já estiver comprovadamente no idioma da aplicação, o botão é desativado (traduzir devolveria o mesmo texto); durante a tradução pode ver o sistema em uso, o tempo decorrido e cancelar;
- **Otimização automática de texto** — o assistente auxilia na reescrita, resumo e melhoria de conteúdos quando solicitado;
- **Detecção inteligente de intenção** — a aplicação reconhece se o pedido é criativo ou factual (ver acima) e ajusta o comportamento do modelo em conformidade, garantindo melhores resultados sem qualquer configuração manual.

## 📋 Logs do Sistema

Tudo o que a app regista fica visível na página **Logs do sistema**, sem precisar de tocar na base de dados:

- **Filtros por nível** — Informação, Avisos, Erros ou Todos;
- **Pesquisa livre** sobre a mensagem do log;
- **Grelha ordenável** por Id, Nível e Mensagem, com paginação (10 registos por página);
- **Diálogo de detalhe** com o registo completo, incluindo a exceção quando existe;
- **Limpeza** — apagar um registo ou todos os registos visíveis (após confirmação).

---

## 🧭 Experiência do Utilizador

A navegação é simples. No menu lateral encontra: **Home** (`/`), **Chat** (`/chat`), **Benchmarks** (`/benchmark-evaluations`, o histórico de avaliações e as exportações), **Qualidade e Métricas** (`/benchmarks`, a vista por pergunta onde os juízes de IA atuam), **Dashboard** (`/dashboard`), **Modelo Carregados** (`/modelos-ollama`), **Prompts** (`/edit-prompts`), **Logs do sistema** (`/system-logs`) e, no rodapé do menu, **Definições** (`/settings`). No cabeçalho há ainda botões para o **README**, o **Plano de Testes** e o guia **Comparar Modelos** (ambos documentos de leitura, disponíveis em português e inglês).

- **Totalmente responsiva**: a interface adapta-se automaticamente a qualquer tamanho de ecrã. Em telemóveis (≤768px), o menu lateral transforma-se num painel deslizante aberto pelo ícone de hambúrguer no cabeçalho; os diálogos, as tabelas de dados, o chat e o divisor de painéis dos benchmarks ajustam-se para garantir uma boa experiência em qualquer dispositivo.
- **Conversar**: abra o Chat, escreva a sua mensagem na caixa de texto e prima <kbd>Enter</kbd> ou o botão de envio. As respostas aparecem em tempo real e cada uma mostra o tempo que demorou. Use **Histórico** para retomar conversas anteriores e **Novo Chat** para começar de novo.
- **Avaliação Automática de Juízes:** no painel **Qualidade e Métricas** (`/benchmarks`), para cada resposta obtida, clique em **"Avaliar automaticamente"** — a aplicação submete o prompt de auditoria ao Gemini e ao OpenRouter, mostra o progresso passo a passo (com opção de cancelar) e abre o ecrã de avaliação já preenchido para, após a sua revisão, guardar os veredictos na BD. Se preferir avaliar manualmente, use **"Copiar prompt"**. Os rótulos dos dois juízes refletem sempre a **origem real** de cada avaliação (automática, externa colada por si, ou "sem resposta").
- **Tema Claro/Escuro**: alterne entre o tema claro e o escuro sempre que preferir. A sua escolha fica **guardada no navegador** e é restaurada automaticamente na próxima visita.
- **Definições**: aceda à página de Definições (`/settings`) para configurar os **juízes de IA** — chaves de API Gemini/OpenRouter, modelo do juiz OpenRouter (escolhido numa lista de radio buttons com o catálogo gratuito do OpenRouter, com **pesquisa** no catálogo e badges de contexto/`free` por modelo) e o toggle de **reasoning (think)** para modelos que suportam raciocínio (gravadas na BD). O botão **Guardar** só fica ativo depois de uma alteração e pede confirmação com o resumo do que mudou — as chaves são mostradas apenas como "definida/removida", nunca o valor.
- **Idioma e reconexão**: o selector **PT | EN** no cabeçalho muda o idioma da interface (e dos documentos) a qualquer momento; se a ligação ao servidor cair, aparece um aviso de reconexão com **Tentar novamente** / **Retomar** — nada se perde, a conversa continua de onde estava.

Comece por enviar uma mensagem no Chat — a aplicação trata de tudo o resto.

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
4. Abrir `http://localhost:5292` (ou `https://localhost:7175`, se iniciar com `dotnet watch run --launch-profile https`). No primeiro arranque, a base de dados SQLite é criada automaticamente.

### Configuração opcional
- **Juízes de IA (chaves + modelo):** a forma mais simples (e a única que funciona em qualquer deploy) é a página **Definições** (`/settings`), com a secção **"Juízes de IA"** — lá pode colar as chaves Gemini/OpenRouter e escolher o modelo do juiz OpenRouter numa **lista de radio buttons** a partir do **catálogo público** (modelos gratuitos; `openrouter/free` é sempre o default e fica fixo no topo) — ou escrever um ID à mão para casos offline/custom. A escolha ativa fica na tabela `Configuracoes` (SQLite); tudo se aplica sem reiniciar. A temperatura dos juízes é **fixa em 0** (determinística) e por isso não é configurável — o que se ajusta automaticamente é a temperatura do *chat* local, não a dos juízes.
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
O repositório inclui um workflow GitHub Actions (`deploy-iis.yml`) que publica a aplicação e faz o deploy para um **self-hosted runner registado apenas na máquina do autor** (a app fica em `http://localhost:4800`). Noutras máquinas, ignore o workflow e corra localmente com `dotnet watch run` — não é necessário qualquer token ou credencial para clonar e testar.

#### Chaves de API no IIS
**Recomendado:** depois do deploy, use a página **Definições** → "Juízes de IA" para colar as chaves — ficam na BD e funcionam sem editar ficheiros. Em alternativa (ou como fallback), configure as chaves no `appsettings.Local.json` do servidor (ex.: `C:\inetpub\wwwroot\OllamaArena\appsettings.Local.json`):

```json
{
  "Security": { "EnableHttpsRedirection": false },
  "ApiKeys": { "Gemini": "<chave>", "OpenRouter": "<chave>" }
}
```

Este ficheiro é **preservado pelo workflow em cada deploy** (fica fora do publish e da limpeza do site) e é relido em runtime (`reloadOnChange`), sem reiniciar o site. **Mantenha o bloco `Security`** — o workflow escreve-o automaticamente em cada deploy. Alternativa: variáveis de ambiente no App Pool do IIS (`ApiKeys__Gemini` / `ApiKeys__OpenRouter`, o `__` é convertido em `:`), que também sobrevivem a redeploys.

### 📚 Documentação técnica
A documentação do projeto está em Markdown — pode consultá-la no repositório ou, após um clone, diretamente no explorador de ficheiros:

| Ficheiro | Conteúdo |
|---|---|
| `OllamaArena/Docs/Funcionalidades.md` | Documentação técnica completa (stack, páginas, serviços, BD, configuração, deploy) |
| `OllamaArena/Docs/code-review.md` | Revisão de código com pontos fortes, bugs e dívida técnica |
| `OllamaArena/Docs/Ollama_CodeReviewer.md` | Perfil do "code reviewer" que produz a revisão acima |
| `OllamaArena/Docs/Relatorio_Analise.md` | Relatório de análise do código (pontos fortes, bugs, dívida técnica) |
| `OllamaArena/Docs/Prompts/` | 11 briefs de texto usados para gerar as ilustrações por IA das capturas |
| `OllamaArena/ComoCompararModelos/Guia_Como_Comparar_Modelos.md` | Guia "como comparar modelos" de forma justa (também em inglês, e legível dentro da app em `/comparar-modelos`) |
| `OllamaArena/PlanoDeTestes/Prompts_Teste_Juiz.md` | Plano de testes com 15 casos para avaliar Bases (também em inglês, e legível em `/plano-de-testes`) |
| `OllamaArena/Docs/Guia_Apresentacao.md` | Como gerar a apresentação `OllamaArena.pptx` a partir das screenshots em `OllamaArena/Screenshots/` |
| `OllamaArena/Docs/OllamaAsAJudge.pptx` | Apresentação de conceito "Ollama como juiz" |
| `OllamaArena/Docs/schema.sql` | DDL de referência — as **7 tabelas** que o runtime cria no arranque (`Prompts`, `Respostas`, `Conversas`, `ConversaMensagens`, `Configuracoes`, `ModelosJuiz`, `Logs`) |

### 📸 Screenshots e apresentação
A pasta **`OllamaArena/Screenshots/`** guarda capturas de ecrã das principais páginas e a apresentação **`OllamaArena.pptx`** (16:9) que as organiza num deck com **template profissional IT** (fundo azul-escuro, acento Fluent), **transições modernas** e **avanço por clique ou após 10 s**. Existe também uma variante gerada a partir de ilustrações por IA — imagens em `Screenshots/AI_Generated/` (a partir dos briefs em `OllamaArena/Docs/Prompts/`) em `Screenshots/OllamaArena_IA.pptx`, com o respetivo script. Para regenerar a apresentação principal (ex.: depois de adicionar capturas), corra:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "OllamaArena\Screenshots\gerar_apresentacao.ps1"
```

Requer **Windows com PowerPoint 2016+** (automação COM). Detalhes em `OllamaArena/Docs/Guia_Apresentacao.md`.


## ℹ️ Nota à navegação

O repositório é público e aberto à comunidade. Sinta-se à vontade para clonar, aprender com a arquitetura de integrações e contribuir para otimizar a tomada de decisão no ecossistema local.
