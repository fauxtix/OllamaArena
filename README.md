# 🦙 Ollama FluentUI Chat & Benchmark Laboratory

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white) ![Blazor Server](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor&logoColor=white) ![FluentUI Blazor v4](https://img.shields.io/badge/FluentUI_Blazor-v4.14.2-0078D4?logo=fluentui&logoColor=white) ![SQLite + Dapper](https://img.shields.io/badge/SQLite-Dapper-003B57?logo=sqlite&logoColor=white) ![Ollama](https://img.shields.io/badge/Ollama-Local_LLMs-7499FF?logo=ollama&logoColor=white) ![PT | EN](https://img.shields.io/badge/Lang-PT%20%7C%20EN-00897B) ![PRs Welcome](https://img.shields.io/badge/PRs-welcome-2ea44f)

> **EN:** [Read this document in English](README.en.md)

A sua central pessoal de inteligência artificial **100% local**. Converse com modelos de IA que correm no seu próprio computador, teste o desempenho de cada um e descubra qual responde mais depressa e com melhor qualidade — tudo através de uma interface moderna e fluida, disponível em português e inglês.

A aplicação é mais do que um simples chat: é um **laboratório de experimentação** que o ajuda a escolher, comparar e aperfeiçoar os modelos de IA que já tem instalados no seu equipamento, sem depender de serviços externos nem de ligação à internet.

---

## 🎯 Motivação, Filosofia e Engenharia de Seleção

Este laboratório nasceu para resolver um desafio prático e diário no ecossistema local: **como escolher o modelo de IA certo para a tarefa certa?** No universo de modelos abertos, o tamanho nem sempre dita a eficácia. Um modelo de `1.5B` ou `3B` pode ter um desempenho factual e de formatação superior ao de um modelo maior para um determinado tipo de prompt. 

Desenvolvido sob uma filosofia de **soberania digital e engenharia orientada a dados**, este projeto público foca-se em dois pilares fundamentais:

### 🔬 1. Seleção Científica do Modelo Ideal (O Core da App)
A aplicação permite cruzar dados e benchmarks para que o utilizador descubra empiricamente qual o modelo local que oferece o melhor equilíbrio para os seus prompts específicos:
- **Pontuação Isolada:** Ao separar de forma cirúrgica o *Score Factual*, o *Score de Formatação* e o *Score Final* (além de outras métricas), a app revela se um modelo pequeno é brilhante em lógica (mas peca no markdown) ou se apenas gera texto bonito sem substância;
- **Contexto Justo (`[CRITICAL CONTEXT]`):** Protege a avaliação de modelos antigos ou pequenos, instruindo os juízes externos (ChatGPT/Gemini) a avaliarem as respostas estritamente com base no ano de treino do modelo local;
- **Arquitetura Híbrida:** O utilizador interage como uma ponte manual, segura e gratuita para recolher feedbacks avançados da nuvem sem gastar um único cêntimo em subscrições ou chaves de API dispendiosas.

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
- **Avaliar a qualidade com dois juízes de IA** — atribuir uma nota de 1 a 5 em 11 critérios (factualidade, formatação, clareza, segurança, etc.) às respostas dos seus modelos, através dos juízes **ChatGPT** e **Gemini**, guardando também a **recomendação** de cada juiz;
- **Analisar os resultados de forma 100% local** — a aplicação gera um resumo inteligente com conclusões e recomendações de desempenho (análise em C# puro, sem depender da nuvem);
- **Exportar os resultados para Excel**, com relatórios agrupados por pergunta, prontos a partilhar;
- Comparar as respostas de **vários modelos lado a lado** para a mesma pergunta, no painel de Qualidade e Métricas;
- Gerir os seus registos, apagando testes individuais ou todo o histórico quando pretender.

## ⚖️ Avaliação dos Dois Juízes (ChatGPT & Gemini)

A aplicação integra um processo estruturado de auditoria externa no painel de **Qualidade e Métricas** para avaliar e pontuar as respostas dadas pelos modelos locais do Ollama, utilizando o ChatGPT e o Gemini como juízes de qualidade:

1. **Copiar para Avaliação:** O utilizador acede à resposta de um modelo específico e clica no botão **"Copiar para avaliação"**. A aplicação gera internamente o prompt de auditoria e coloca-o no clipboard.
2. **Contexto Crítico Injetado:** O prompt gerado inclui automaticamente metadados inteligentes (como o ano de treino do modelo local) sob a marca `[CRITICAL CONTEXT]`, instruindo o juiz externo a não penalizar o modelo por falta de conhecimento de eventos futuros.
3. **Processamento nos Browsers:** O utilizador abre manualmente o seu navegador de internet, acede às sessões do ChatGPT e do Gemini, e faz <kbd>Ctrl</kbd>+<kbd>V</kbd> para submeter o prompt a ambos os juízes.
4. **Abertura do Ecrã de Avaliação:** Após obter as respostas dos juízes no browser, o utilizador regressa à aplicação e clica no botão **"Avaliação"** (situado junto aos contadores dos juízes).
5. **Colagem do Output Bruto:** No ecrã de avaliação, o utilizador cola o texto copiado do browser diretamente nos campos **"Feedback (Cole aqui o output bruto do Gemini/ChatGPT)"**.
6. **Extração Automática de Métricas:** O sistema analisa o texto bruto colado e preenche automaticamente as **Métricas de Avaliação (Escala 1-5)**, incluindo critérios como *Factual, Formatação, Compliance, Relevância, Tom, Concisão, Clareza, Legibilidade, Halo Effect, Segurança* e a nota *Global*; também extrai a **RECOMMENDATION** de cada juiz, que fica guardada na Base de Dados e é apresentada em destaque no formulário de avaliação e no diálogo de feedbacks.
7. **Tradução e Consolidação:** O utilizador pode utilizar a opção **"Traduzir Feedback"** para passar as análises para a língua ativa na interface (português ou inglês) e, por fim, clica em **"Guardar avaliação"** para persistir todos os dados permanentemente na Base de Dados.

## 💬 Interface de Chat

- **Respostas em tempo real (streaming)** — o texto aparece no ecrã palavra a palavra, à medida que o modelo o gera;
- **Formatação** — títulos, listas, tabelas e blocos de código são apresentados de forma limpa e legível;
- **Histórico de conversas** — guarda e reabre as suas conversas anteriores em qualquer altura;
- **Novo chat com um clique** — começa uma conversa do zero instantaneamente, libertando os recursos do computador;
- **Cronómetro integrado** — cada resposta mostra quanto tempo demorou, para monitorizar o desempenho;
- **Cancelamento a qualquer momento** — interromper uma resposta;
- **Indicador de compatibilidade gráfica** — a aplicação avisa-o se o modelo cabe na memória da sua placa gráfica ou se vai correr mais devagar no processador;
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

A navegação é simples. No menu lateral encontra todas as secções da aplicação: **Chat**, **Benchmarks**, **Qualidade e Métricas**, **Modelos carregados**, **Prompts** e **Logs**.

- **Conversar**: abra o Chat, escreva a sua mensagem na caixa de texto e prima <kbd>Enter</kbd> ou o botão de envio. As respostas aparecem em tempo real e cada uma mostra o tempo que demorou. Use **Histórico** para retomar conversas anteriores e **Novo Chat** para começar de novo.
- **Processo de Avaliação de Juízes:** Para cada resposta obtida, selecione a opção **"Copia para avaliação"**. Serão abertas duas novas abas no browser. Aceda a cada uma e faça <kbd>Ctrl</kbd>+<kbd>V</kbd>. De seguida, selecione a opção **"Avaliação"** na aplicação. Copie a resposta do primeiro juiz no browser e cole-a no ecrã de avaliação da app. Repita o processo para o segundo juiz para guardar os veredictos na BD.
- **Tema Claro/Escuro**: alterne entre o tema claro e o escuro sempre que preferir. A sua escolha fica **guardada no navegador** e é restaurada automaticamente na próxima visita.
- **Definições**: aceda à página de Definições para gerir os seus modelos de IA (definir o modelo predefinido e controlar os disponíveis) e personalizar a aparência da aplicação.

Comece por enviar uma mensagem no Chat — a aplicação trata de tudo o resto.

## ℹ️ Nota à navegação

O repositório é público e aberto à comunidade. Sinta-se à vontade para clonar, aprender com a arquitetura de integrações e contribuir para otimizar a tomada de decisão no ecossistema local.
