-- Schema SQLite da aplicacao OllamaArena
-- Reune as 7 tabelas usadas pela app: Prompts, Respostas, Conversas, ConversaMensagens,
-- Configuracoes, ModelosJuiz e Logs.
-- O schema tambem e criado automaticamente no arranque (DatabaseSchemaInitializer.EnsureSchema);
-- este ficheiro e uma referencia executavel antes da primeira utilizacao.

PRAGMA foreign_keys = ON;

-- ---------------------------------------------------------------------------
-- Prompts: prompts de benchmark criados na página de benchmarks.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Prompts" (
    "Id"          INTEGER PRIMARY KEY AUTOINCREMENT,
    "Descricao"   TEXT,
    "TextoPrompt" TEXT NOT NULL,
    "DataCriacao" TEXT NOT NULL,
    "Temperatura" REAL
);

-- ---------------------------------------------------------------------------
-- Respostas: respostas de cada modelo por prompt, com métricas e avaliações
-- (ratings 1-5) dos dois juízes: Gemini e OpenRouter. Cada juiz avalia 12
-- métricas (Factual, Formatação, Compliance, Relevância, Tom, Concisão,
-- Clareza, Legibilidade, Efeito Halo, Segurança, Consistência Idiomática e
-- Detecção de Loop) + a nota Global.
-- ON DELETE CASCADE: apagar um prompt remove as respostas associadas.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Respostas" (
    "Id"                         INTEGER PRIMARY KEY AUTOINCREMENT,
    "PromptId"                   INTEGER NOT NULL,
    "NomeModelo"                 TEXT NOT NULL,
    "TextoResposta"              TEXT NOT NULL,
    "TokensPorSegundo"           REAL,
    "TempoPuroMs"                REAL,
    "TempoCargaMs"               REAL,
    "TempoProcessamento"         REAL,
    "TamanhoTokens"              INTEGER,

    -- Avaliacao geral / ratings finais
    "GeminiRating"               NUMERIC,
    "GeminiFeedback"             TEXT,
    "GeminiRecommendation"       TEXT,
    "OpenRouterRating"           NUMERIC,
    "OpenRouterFeedback"         TEXT,
    "OpenRouterRecommendation"   TEXT,

    -- Metricas especificas do Gemini
    "GeminiFactualRating"           INTEGER,
    "GeminiFormattingRating"        INTEGER,
    "GeminiComplianceRating"        INTEGER,
    "GeminiRelevanceRating"         INTEGER,
    "GeminiToneRating"              INTEGER,
    "GeminiConcisenessRating"       INTEGER,
    "GeminiClarityRating"           INTEGER,
    "GeminiReadabilityRating"       INTEGER,
    "GeminiHaloEffectRating"        INTEGER,
    "GeminiSafetyRating"            INTEGER,
    "GeminiLanguageConsistencyRating" INTEGER,
    "GeminiLoopDetectionRating"     INTEGER,

    -- Metricas especificas do OpenRouter
    "OpenRouterFactualRating"           INTEGER,
    "OpenRouterFormattingRating"        INTEGER,
    "OpenRouterComplianceRating"        INTEGER,
    "OpenRouterRelevanceRating"         INTEGER,
    "OpenRouterToneRating"              INTEGER,
    "OpenRouterConcisenessRating"       INTEGER,
    "OpenRouterClarityRating"           INTEGER,
    "OpenRouterReadabilityRating"       INTEGER,
    "OpenRouterHaloEffectRating"        INTEGER,
    "OpenRouterSafetyRating"            INTEGER,
    "OpenRouterLanguageConsistencyRating" INTEGER,
    "OpenRouterLoopDetectionRating"     INTEGER,

    FOREIGN KEY ("PromptId") REFERENCES "Prompts" ("Id") ON DELETE CASCADE
);

-- ---------------------------------------------------------------------------
-- Conversas: conversas do chat persistidas em SQLite (HistoryPanel).
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Conversas" (
    "Id"                  INTEGER PRIMARY KEY AUTOINCREMENT,
    "Titulo"              TEXT,
    "NomeModelo"          TEXT,
    "DataCriacao"         TEXT,
    "DataUltimaAtividade" TEXT
);

-- ---------------------------------------------------------------------------
-- ConversaMensagens: mensagens de cada conversa, com reasoning quando o
-- modelo devolveu reasoning_content.
-- ON DELETE CASCADE: apagar uma conversa remove as suas mensagens.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "ConversaMensagens" (
    "Id"             INTEGER PRIMARY KEY AUTOINCREMENT,
    "ConversationId" INTEGER NOT NULL,
    "Role"           TEXT NOT NULL,
    "Content"        TEXT NOT NULL,
    "Reasoning"      TEXT,
    "Temperature"    REAL,
    "ElapsedTime"    TEXT,
    "Timestamp"      TEXT,
    FOREIGN KEY ("ConversationId") REFERENCES "Conversas" ("Id") ON DELETE CASCADE
);

-- ---------------------------------------------------------------------------
-- Configuracoes: deficoes configuraveis pela pagina Settings (chave/valor).
-- Exemplos: ApiKeys:Gemini, ApiKeys:OpenRouter, AutomatedJudge:OpenRouterModel,
-- Chat:EnableReasoning.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Configuracoes" (
    "Chave" TEXT PRIMARY KEY,
    "Valor" TEXT NOT NULL
);

-- ---------------------------------------------------------------------------
-- ModelosJuiz: registry de modelos ":free" do OpenRouter para a combo-box
-- da pagina Settings. Tabela legada — a UI hoje usa o catalogo ao vivo
-- (OpenRouterCatalogService), mas continua a ser semeada no primeiro arranque.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "ModelosJuiz" (
    "Id"          INTEGER PRIMARY KEY AUTOINCREMENT,
    "Nome"        TEXT NOT NULL UNIQUE,
    "DataCriacao" TEXT NOT NULL
);

-- ---------------------------------------------------------------------------
-- Logs: registos do Serilog (Serilog.Sinks.SQLite), mesma base de dados.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS "Logs" (
    "Id"              INTEGER PRIMARY KEY AUTOINCREMENT,
    "Timestamp"       TEXT,
    "Level"           TEXT,
    "Exception"       TEXT,
    "RenderedMessage" TEXT,
    "Properties"      TEXT
);
