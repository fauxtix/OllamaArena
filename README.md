# Ollama FluentUI Chat & Benchmark Laboratory 🚀

Uma aplicação web moderna desenvolvida em **.NET 8 / Blazor** que funciona como um laboratório de testes locais para Modelos de Linguagem Pequenos (SLMs). A aplicação combina uma interface de chat fluida em tempo real com um painel de telemetria avançado e avaliação de qualidade (*LLM-as-a-Judge*).

---

## 💻 Notas Importantes sobre o Hardware e Desempenho

### Ambiente de Teste Inicial (Máquina Antiga)
Este laboratório foi projetado e testado inicialmente numa **máquina antiga limitada a apenas 1GB de VRAM**. Sob estas restrições estritas, o sistema funciona de forma híbrida: o **Ollama** faz a gestão inteligente da memória, enviando o que não cabe na placa gráfica para processamento direto na memória RAM e processador (CPU) do computador.

### Upgrade Recomendado para Modelos Maiores
Para obter respostas com maior maturidade intelectual e menor índice de alucinações, **devem ser utilizadas placas gráficas (GPUs) modernas e dedicadas**. Um upgrade de hardware permitirá carregar localmente modelos muito mais potentes, que exigem maior capacidade de processamento gráfico para entregar resultados de qualidade superior em tempo útil.

---

## 🧠 Características Principais

### 1. Chat Local em Tempo Real (Modo Offline)
- Interface de chat interativa desenvolvida com **Microsoft FluentUI Blazor Components v4**.
- Processamento de texto por fluxo de rede (*Streaming HTTP*) de alto desempenho com processamento por blocos brutos (`buffer`).
- Configuração determinista controlada (`temperature: 0.3` / `repeat_penalty: 1.2`), mitigando alucinações ou loops infinitos de texto em modelos de pequena escala.

### 2. Laboratório de Benchmarks & Telemetria
- Captura em nanossegundos das métricas oficiais do Ollama quando o fluxo termina (`done = true`).
- Gravação automática de estatísticas críticas na base de dados:
  - **Tokens por Segundo (T/s)**: Desempenho real de geração de texto.
  - **Tempo Puro (Eval Ms)**: Velocidade estrita do processamento de tokens.
  - **Tempo de Carga (Load Ms)**: O tempo que o Ollama demora a carregar/paginar o modelo para a memória.
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

## 📊 Modelos Utilizados nos Testes

Os seguintes modelos de pequena escala (SLMs) foram escolhidos especificamente para avaliar o comportamento do ecossistema sob cenários de baixa memória e paginação por CPU:
- **`qwen2.5:0.5b`** (Ultrarápido; devido ao tamanho reduzido, executa quase na totalidade dentro do teto de 1GB de VRAM).
- **`llama3.2:1b`** (Equilibrado; modelo compacto e eficiente da Meta para lógica simples).
- **`qwen2.5:1.5b`** (Excelente coesão estrutural em inglês; começa a exigir *offloading* acrescido para o CPU).
- **`gemma2:2b`** (Modelo da Google surpreendentemente forte em conhecimento geral para o tamanho que tem).
- **`phi4-mini:latest`** (O modelo mais pesado da lista, com cerca de 3.8B de parâmetros; corre maioritariamente no CPU nesta máquina antiga, demonstrando alta precisão factual em inglês, mas com um tempo de processamento mais elevado).

---

## ⚙️ Guia de Configuração do Ambiente

### 1. Como Instalar o Ollama
Para correr os modelos de inteligência artificial localmente na sua máquina, siga os passos conforme o seu sistema operativo:

- **Windows**: Transfira o instalador oficial em [://ollama.com](https://://ollama.com). Execute o ficheiro `.exe` e siga o assistente até ao fim.
- **Linux**: Abra o terminal e execute o comando oficial de instalação automática:
  ```bash
  curl -fsSL https://ollama.com | sh
  ```
- **macOS**: Transfira o ficheiro `.zip` oficial no site do Ollama, descomprima-o e arraste a aplicação para a pasta *Applications*.

### 2. Como Carregar os Modelos para Testar
Abra o seu terminal (CMD, PowerShell ou Bash) e execute os seguintes comandos para descarregar a suite exata de modelos utilizada nos nossos testes:

```bash
ollama pull qwen2.5:0.5b
ollama pull llama3.2:1b
ollama pull qwen2.5:1.5b
ollama pull gemma2:2b
ollama pull phi4-mini:latest
```
Para verificar a lista de modelos guardados com sucesso no seu disco, execute:
```bash
ollama list
```

---

## 🚀 Como Executar o Projeto

1. Certifique-se de que o daemon do Ollama está ativo no sistema (`ollama serve`).
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
Para obter respostas consistentes, envie exatamente o seguinte prompt estruturado para as interfaces do Gemini e do ChatGPT:

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
