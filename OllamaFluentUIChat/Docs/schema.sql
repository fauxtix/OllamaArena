-- Schema SQLite da aplicacao OllamaFluentUIChat
-- Reune as 3 tabelas usadas pela app: Prompts, Respostas e Logs.
-- Executar num novo ficheiro de base de dados antes da primeira utilizacao.

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
-- (ratings 1-5) dos dois juízes: Gemini e OpenRouter.
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
    "GeminiFactualRating"        INTEGER,
    "GeminiFormattingRating"     INTEGER,
    "GeminiComplianceRating"     INTEGER,
    "GeminiRelevanceRating"      INTEGER,
    "GeminiToneRating"           INTEGER,
    "GeminiConcisenessRating"    INTEGER,
    "GeminiClarityRating"        INTEGER,
    "GeminiReadabilityRating"    INTEGER,
    "GeminiHaloEffectRating"     INTEGER,
    "GeminiSafetyRating"         INTEGER,

    -- Metricas especificas do OpenRouter
    "OpenRouterFactualRating"    INTEGER,
    "OpenRouterFormattingRating" INTEGER,
    "OpenRouterComplianceRating" INTEGER,
    "OpenRouterRelevanceRating"  INTEGER,
    "OpenRouterToneRating"       INTEGER,
    "OpenRouterConcisenessRating" INTEGER,
    "OpenRouterClarityRating"    INTEGER,
    "OpenRouterReadabilityRating" INTEGER,
    "OpenRouterHaloEffectRating" INTEGER,
    "OpenRouterSafetyRating"     INTEGER,

    FOREIGN KEY ("PromptId") REFERENCES "Prompts" ("Id") ON DELETE CASCADE
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
