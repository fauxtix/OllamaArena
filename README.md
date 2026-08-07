# 🦙 Ollama FluentUI Chat & Benchmark Laboratory

![Ollama FluentUI Chat & Benchmark Laboratory](assets/readme-banner.svg)

![.NET 10](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white) ![Blazor Server](https://img.shields.io/badge/Blazor-Server-512BD4?logo=blazor&logoColor=white) ![FluentUI Blazor v4](https://img.shields.io/badge/FluentUI_Blazor-v4.14.2-0078D4?logo=fluentui&logoColor=white) ![SQLite + Dapper](https://img.shields.io/badge/SQLite-Dapper-003B57?logo=sqlite&logoColor=white) ![Ollama](https://img.shields.io/badge/Ollama-Local_LLMs-7499FF?logo=ollama&logoColor=white) ![PT | EN](https://img.shields.io/badge/Lang-PT%20%7C%20EN-00897B) ![PRs Welcome](https://img.shields.io/badge/PRs-welcome-2ea44f)

> **EN:** [Read this document in English](README.en.md)

A sua central pessoal de inteligência artificial **100% local**. Converse com modelos de IA que correm no seu próprio computador, teste o desempenho de cada um e descubra qual responde mais depressa e com melhor qualidade — tudo através de uma interface moderna e fluida, disponível em português e inglês.

A aplicação é mais do que um simples chat: é um **laboratório de experimentação** que o ajuda a escolher, comparar e aperfeiçoar os modelos de IA que já tem instalados no seu equipamento, sem depender de serviços externos nem de ligação à internet.

---

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

## 💬 Interface de Chat Avançada

Uma experiência de conversação moderna e confortável:

- **Respostas em tempo real (streaming)** — o texto aparece no ecrã palavra a palavra, à medida que o modelo o gera;
- **Formatação inteligente** — títulos, listas, tabelas e blocos de código são apresentados de forma limpa e legível;
- **Histórico de conversas** — guarda e reabre as suas conversas anteriores em qualquer altura;
- **Novo chat com um clique** — começa uma conversa do zero instantaneamente, libertando os recursos do computador;
- **Cronómetro integrado** — cada resposta mostra quanto tempo demorou, para monitorizar o desempenho;
- **Cancelamento a qualquer momento** — interrompa uma resposta com um simples botão;
- **Indicador de compatibilidade gráfica** — a aplicação avisa-o se o modelo cabe na memória da sua placa gráfica ou se vai correr mais devagar no processador;
- **Comportamento ajustado automaticamente** — a aplicação deteta o tipo de pedido (criativo, factual, tradução) e afina automaticamente o modelo para obter o melhor resultado em cada situação.

## 🤖 Gestão de Modelos de IA

Sem complicações, com tudo à distância de um clique:

- **Alternância instantânea de modelos** — mude de modelo de IA no meio da conversa, diretamente a partir do chat;
- **Página de Modelos Carregados** — consulte, num relance, todos os modelos instalados no seu computador, com informação útil sobre cada um: família, dimensão (parâmetros), nível de quantização, ano de treino, contexto máximo suportado, espaço ocupado no disco e compatibilidade com a sua placa gráfica;
- **Definições centralizadas** — defina o modelo predefinido e controle quais os modelos disponíveis para utilização, tudo na página de Definições;
- **Preferências guardadas no navegador** — o seu modelo favorito fica memorizado entre sessões.

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

A navegação é simples e intuitiva. No menu lateral encontra todas as secções da aplicação: **Chat**, **Benchmarks**, **Qualidade e Métricas**, **Modelos carregados**, **Prompts** e **Logs**.

- **Conversar**: abra o Chat, escreva a sua mensagem na caixa de texto e prima <kbd>Enter</kbd> ou o botão de envio. As respostas aparecem em tempo real e cada uma mostra o tempo que demorou. Use **Histórico** para retomar conversas anteriores e **Novo Chat** para começar de novo.
- **Tema Claro/Escuro**: alterne entre o tema claro e o escuro sempre que preferir. A sua escolha fica **guardada no navegador** e é restaurada automaticamente na próxima visita.
- **Definições**: aceda à página de Definições para gerir os seus modelos de IA (definir o modelo predefinido e controlar os disponíveis) e personalizar a aparência da aplicação.

Comece por enviar uma mensagem no Chat — a aplicação trata de tudo o resto.
