# Ollama FluentUI Chat & Benchmark Laboratory 🚀

Uma aplicação web moderna desenvolvida em **.NET 8 / Blazor** que funciona como um laboratório de testes locais para Modelos de Linguagem Pequenos (SLMs). A aplicação combina uma interface de chat fluida em tempo real com um painel de telemetria avançado e avaliação de qualidade (*LLM-as-a-Judge*).

O projeto foi desenhado sob restrições estritas de hardware, sendo otimizado para correr eficientemente em ambientes com **1GB de VRAM**, gerindo a paginação de memória de forma inteligente entre o GPU e o CPU através do **Ollama**.

---

## 🧠 Características Principais

### 1. Chat Local em Tempo Real (Modo Offline)
- Interface de chat interativa desenvolvida com **Microsoft FluentUI Blazor Components v4**.
- Processamento de texto por fluxo de rede (*Streaming HTTP*) de alto desempenho com processamento por blocos brutos (`buffer`).
- Configuração determinista controlada (`temperature: 0.3` / `repeat_penalty: 1.2`), mitigando alucinações históricas ou loops infinitos de texto em modelos pequenos.

### 2. Laboratório de Benchmarks & Telemetria
- Captura em nanossegundos das métricas oficiais do Ollama quando o fluxo termina (`done = true`).
- Gravação automática de estatísticas críticas na base de dados:
  - **Tokens por Segundo (T/s)**: Desempenho real de geração de texto.
  - **Tempo Puro (Eval Ms)**: Velocidade estrita do processamento de tokens.
  - **Tempo de Carga (Load Ms)**: O tempo que o Ollama demora a carregar/paginar o modelo para a RAM/VRAM.
  - **Tamanho (Count)**: Contagem total de tokens gerados.

### 3. Painel de Análise Master-Detail (Layout Proporcional 1/3 e 2/3)
- **Painel Esquerdo (1/3)**: Lista cronológica de prompts executados, dispostos em `FluentCard` com efeitos hover e truncagem inteligente.
- **Painel Direito (2/3)**: Cabeçalho fixo com o prompt selecionado e área de scroll independente para os cartões de resposta das IAs lado a lado.
- Interface dividida por um **`FluentSplitter`** responsivo, adaptável a qualquer monitor ou portátil.

### 4. Avaliação Cruzada (LLM-as-a-Judge)
- Painel integrado para introdução de métricas de qualidade qualitativa baseadas em modelos de fronteira (**Google Gemini** e **OpenAI ChatGPT**).
- Permite atribuir classificações de 1 a 5 (`FluentNumberField`) e justificações de erros/alucinações (`FluentTextArea` de 3 linhas com redimensionamento vertical).
- Persistência imediata via **Dapper** para fechar o ciclo de análise: *Velocidade Local vs. Qualidade Global*.

---

## 🛠️ Stack Tecnológica

- **Frontend**: Blazor Web Assembly / Server (InteractiveServer Mode)
- **Componentes UI**: Microsoft FluentUI Blazor Library v4.1.2
- **Motor Local de IA**: Ollama API (`/api/chat`)
- **Base de Dados**: SQLite (Leve, local e em ficheiro)
- **Micro-ORM**: Dapper (Mapeamento de alto desempenho por reflexão de objetos)

---

## 📊 Modelos Atualmente em Teste (Benchmark Base)

Devido ao teto estrito de 1GB de VRAM, o laboratório foca-se na monitorização e comportamento dos seguintes modelos:
- `qwen2.5:0.5b` (Ultrarápido, executa quase integralmente na VRAM)
- `llama3.2:1b` (Equilibrado, excelente para lógica simples)
- `qwen2.5:1.5b` (Excelente coesão em inglês, propenso a offloading)
- `gemma2:2b` (Surpreendentemente forte em conhecimento geral offline)
- `phi4-mini:latest` (O mais pesado, corre maioritariamente em CPU, alta precisão factual em inglês)

---

## 🏗️ Arquitetura de Dados (SQLite)

O repositório utiliza o Dapper para persistir as entidades através de uma relação Um-para-Muitos (*One-to-Many*):

### Tabela: `Prompts`
- `Id` (INTEGER PRIMARY KEY)
- `TextoPrompt` (TEXT)
- `DataCriacao` (DATETIME)

### Tabela: `Respostas`
- `Id` (INTEGER PRIMARY KEY)
- `PromptId` (INTEGER FK &rightarrow; Prompts)
- `ModeloNome` (TEXT)
- `TextoResposta` (TEXT)
- `TokensPorSegundo` (REAL)
- `TempoPuroMs` (REAL)
- `TempoCargaMs` (REAL)
- `TamanhoTokens` (INTEGER)
- `GeminiRating` (INTEGER NULL)
- `GeminiFeedback` (TEXT)
- `ChatGptRating` (INTEGER NULL)
- `ChatGptFeedback` (TEXT)

---

## ⚙️ Guia de Configuração do Ambiente

### 1. Como Instalar o Ollama
Para correr os modelos de inteligência artificial localmente na sua máquina, siga os passos conforme o seu sistema operativo:

- **Windows**: Transfira o instalador oficial em [://ollama.com](https://://ollama.com). Execute o ficheiro `.exe` e siga o assistente até ao fim. O Ollama passará a correr em segundo plano na barra de tarefas (systray).
- **Linux**: Abra o terminal e execute o comando oficial de instalação automática:
  ```bash
  curl -fsSL https://ollama.com | sh
  ```
- **macOS**: Transfira o ficheiro `.zip` oficial no site do Ollama, descomprima-o e arraste a aplicação para a pasta *Applications*.

*Nota para Máquinas com Baixa VRAM (1GB):* O Ollama deteta automaticamente o seu hardware. Se o modelo exceder 1GB, ele dividirá o processamento dinamicamente com o processador (CPU), garantindo que a aplicação não crasha por falta de memória de vídeo.

### 2. Como Carregar os Modelos para Testar
Antes de abrir a aplicação Blazor, precisa de descarregar os 5 modelos configurados para o laboratório de testes. Abra o seu terminal (CMD, PowerShell ou Bash) e execute os seguintes comandos, um de cada vez:

```bash
# Descarregar os modelos mais leves (0.5B e 1B)
ollama pull qwen2.5:0.5b
ollama pull llama3.2:1b

# Descarregar os modelos intermédios (1.5B e 2B)
ollama pull qwen2.5:1.5b
ollama pull gemma2:2b

# Descarregar o modelo de maior precisão do laboratório (3.8B)
ollama pull phi4-mini:latest
```
Para verificar se os modelos foram guardados com sucesso no seu disco, execute:
```bash
ollama list
```

---

## 🚀 Como Executar o Projeto

1. Certifique-se de que o daemon do Ollama está ativo no sistema.
2. Configure a Connection String do SQLite no ficheiro `appsettings.json`.
3. Abra a pasta do projeto no terminal e execute o comando .NET:
   ```bash
   dotnet watch run
   ```
4. Aceda ao endereço local indicado no terminal para interagir com a interface.

---

## ⚖️ Processo de Avaliação Cruzada (LLM-as-a-Judge)

O objetivo desta etapa é avaliar a **qualidade factual** das respostas geradas pelos modelos pequenos, comparando-as com o discernimento de modelos de fronteira (*Frontier Models*).

### 1. Quais são os campos a preencher?
No painel direito (2/3) da aplicação, após selecionar um prompt, terá acesso a 2 controlos de input por cada cartão de modelo:
- **Google Gemini**: Rating (campo numérico de 1 a 5) e Problemas Encontrados (`FluentTextArea` adaptado para 3 linhas com redimensionamento).
- **OpenAI ChatGPT**: Rating (campo numérico de 1 a 5) e Problemas Encontrados (`FluentTextArea` adaptado para 3 linhas com redimensionamento).

### 2. O Processo de Trabalho
1. Aceda ao ecrã de **Análise de Benchmarks**.
2. Selecione um Prompt na barra lateral esquerda (1/3).
3. No painel direito, copie o **Prompt** e a **Resposta** gerada pelo modelo local que deseja avaliar.
4. Abra a interface web do Google Gemini ou do ChatGPT e submeta o prompt de avaliação (descrito abaixo).
5. Copie a nota e o resumo dos problemas gerados pelos juízes de IA e cole-os nos respetivos campos do seu painel.
6. Clique em **"Gravar Avaliação"** para persistir as notas no SQLite através do Dapper utilizando reflexão automática de propriedades (`WHERE Id = @Id`).

### 3. O Prompt Padrão para dar ao Gemini / ChatGPT
Para obter respostas consistentes, curtas e fáceis de transcrever para a sua aplicação, envie exatamente o seguinte prompt estruturado para o Gemini e para o ChatGPT:

> **Prompt de Avaliação (Juiz de IA):**
> 
> "Age como um juiz rigoroso de Inteligência Artificial. Analisa a qualidade factual, lógica e gramatical da resposta gerada por um modelo local pequeno para o prompt fornecido.
> 
> **Prompt Original Submetido:**
> [Colar aqui o 'Prompt Executado' da sua app]
> 
> **Resposta Gerada pelo Modelo Local:**
> [Colar aqui o texto da resposta do cartão da sua app]
> 
> **Instruções de Resposta:**
> Dá-me estritamente uma nota de 1 a 5 (onde 1 é péssimo/alucinação total e 5 é perfeito/factual) seguido de uma descrição muito breve, com um máximo de duas frases, apontando onde estão os principais problemas (alucinações, inversão de datas, omissões ou erros de tradução). Se não houver problemas, elogia de forma concisa."

---

## 📝 Boas Práticas Identificadas no Laboratório
- **Prompting em Inglês**: Para modelos abaixo de 4B de parâmetros, prompts em inglês reduzem o consumo de atenção cognitiva da IA com traduções em tempo real, aumentando a precisão factual em até 80%.
- **Temperatura Zero/Baixa**: Manter `temperature: 0.3` ou inferior é fundamental para testes comparativos justos (benchmarks científicos deterministas).
- **Segurança de Componentes**: Utilização de `StopPropagation="true"` no Blazor ao acionar botões de eliminação dentro de cartões clicáveis, evitando disparos em cascata na UI.
