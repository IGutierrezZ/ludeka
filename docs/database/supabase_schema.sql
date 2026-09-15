-- ==============================================================================
-- ADVERTENCIA — ESTE ARCHIVO NO ES LA FUENTE DE VERDAD DEL ESQUEMA
-- ==============================================================================
-- Este script está DESACTUALIZADO respecto al modelo real. Le faltan las tablas
-- BggCatalogStaging, SocialInboxItems, MonitoredSocialAccounts y
-- GamePriceSnapshots, y contiene columnas obsoletas.
--
-- La única fuente de verdad del esquema son las migraciones de Entity Framework
-- Core en src/Ludeka.Infrastructure/Migrations/, aplicadas automáticamente por
-- MigrateAsync() al arrancar la aplicación contra PostgreSQL.
--
-- NO ejecutar este script a mano contra Supabase: produce un esquema incompatible
-- con EF Core. Su regeneración o retirada está planificada en el INC-48.
-- ==============================================================================

-- ==============================================================================
-- Ludeka / Ludist — Esquema Maestro DDL para PostgreSQL (Supabase)
-- Versión: 1.0.0 (INC-38)
-- Compatible con: EF Core 10, Npgsql, Supabase SQL Editor
-- ==============================================================================

-- Extensiones requeridas en PostgreSQL / Supabase
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- 1. Tabla: Games (Catálogo Maestro)
CREATE TABLE IF NOT EXISTS "Games" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "Slug" text NOT NULL,
    "BggId" integer,
    "SpanishTitle" text NOT NULL,
    "OriginalTitle" text NOT NULL,
    "Designer" text NOT NULL,
    "Publisher" text NOT NULL,
    "ReleaseYear" integer NOT NULL,
    "CoverUrl" text NOT NULL,
    "Type" integer NOT NULL DEFAULT 0,
    "BaseGameId" uuid REFERENCES "Games"("Id") ON DELETE RESTRICT,
    "LanguageDependence" integer NOT NULL DEFAULT 0,
    "TableFootprint" integer NOT NULL DEFAULT 0,
    "Age_BoxMinAge" integer NOT NULL DEFAULT 0,
    "Age_CommunityMinAge" integer,
    "Duration_MinMinutes" integer NOT NULL DEFAULT 0,
    "Duration_MaxMinutes" integer NOT NULL DEFAULT 0,
    "Duration_EstimatedMinutesPerPerson" integer NOT NULL DEFAULT 0,
    "Duration_Pace" integer NOT NULL DEFAULT 0,
    "ImpactTags" text[] NOT NULL DEFAULT '{}',
    "Scalability" jsonb NOT NULL DEFAULT '[]',
    "Sleeves" jsonb NOT NULL DEFAULT '[]',
    "PurchaseLinks" jsonb NOT NULL DEFAULT '[]',
    "AiSummary" jsonb,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "UpdatedAt" timestamp with time zone
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Games_Slug" ON "Games"("Slug");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Games_BggId" ON "Games"("BggId");
CREATE INDEX IF NOT EXISTS "IX_Games_SpanishTitle" ON "Games"("SpanishTitle");
CREATE INDEX IF NOT EXISTS "IX_Games_OriginalTitle" ON "Games"("OriginalTitle");
CREATE INDEX IF NOT EXISTS "IX_Games_BaseGameId" ON "Games"("BaseGameId");
CREATE INDEX IF NOT EXISTS "IX_Games_Type" ON "Games"("Type");

-- 2. Tabla: UserCollectionItems (Colección en Ludoteca)
CREATE TABLE IF NOT EXISTS "UserCollectionItems" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId" text NOT NULL,
    "GameId" uuid REFERENCES "Games"("Id") ON DELETE CASCADE,
    "BggId" integer,
    "Status" integer,
    "IsPlayed" boolean NOT NULL DEFAULT false,
    "AddedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "Notes" text
);

CREATE INDEX IF NOT EXISTS "IX_UserCollectionItems_UserId_GameId" ON "UserCollectionItems"("UserId", "GameId");
CREATE INDEX IF NOT EXISTS "IX_UserCollectionItems_UserId_BggId" ON "UserCollectionItems"("UserId", "BggId");
CREATE INDEX IF NOT EXISTS "IX_UserCollectionItems_UserId_Status" ON "UserCollectionItems"("UserId", "Status");
CREATE INDEX IF NOT EXISTS "IX_UserCollectionItems_UserId_IsPlayed" ON "UserCollectionItems"("UserId", "IsPlayed");

-- 3. Tabla: GamePlayLogs (Diario de Partidas)
CREATE TABLE IF NOT EXISTS "GamePlayLogs" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId" varchar(100) NOT NULL,
    "GameId" uuid NOT NULL REFERENCES "Games"("Id") ON DELETE CASCADE,
    "PlayDate" timestamp with time zone NOT NULL,
    "PlayerCount" integer NOT NULL,
    "DurationMinutes" integer,
    "Location" varchar(150) NOT NULL,
    "IsWon" boolean NOT NULL DEFAULT false,
    "PlayerScore" text,
    "Comment" varchar(1000),
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now())
);

CREATE INDEX IF NOT EXISTS "IX_GamePlayLogs_UserId_PlayDate" ON "GamePlayLogs"("UserId", "PlayDate");
CREATE INDEX IF NOT EXISTS "IX_GamePlayLogs_GameId" ON "GamePlayLogs"("GameId");

-- 4. Tabla: PendingBggImports (Cola de Ingesta BGG)
CREATE TABLE IF NOT EXISTS "PendingBggImports" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "BggId" integer NOT NULL,
    "Title" text NOT NULL,
    "YearPublished" integer,
    "CoverUrl" text,
    "Status" integer NOT NULL DEFAULT 0,
    "RequestedCount" integer NOT NULL DEFAULT 1,
    "LastRequestedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "ErrorMessage" text
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_PendingBggImports_BggId" ON "PendingBggImports"("BggId");
CREATE INDEX IF NOT EXISTS "IX_PendingBggImports_Status_RequestedCount" ON "PendingBggImports"("Status", "RequestedCount");
CREATE INDEX IF NOT EXISTS "IX_PendingBggImports_CreatedAt" ON "PendingBggImports"("CreatedAt");

-- 5. Tabla: GameLoans (Módulo de Préstamos)
CREATE TABLE IF NOT EXISTS "GameLoans" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId" text NOT NULL,
    "GameId" uuid NOT NULL REFERENCES "Games"("Id") ON DELETE CASCADE,
    "BorrowerName" text NOT NULL,
    "LoanDate" timestamp with time zone NOT NULL,
    "DueDate" timestamp with time zone,
    "IsReturned" boolean NOT NULL DEFAULT false,
    "ReturnDate" timestamp with time zone,
    "Notes" text
);

CREATE INDEX IF NOT EXISTS "IX_GameLoans_UserId_IsReturned" ON "GameLoans"("UserId", "IsReturned");
CREATE INDEX IF NOT EXISTS "IX_GameLoans_UserId_GameId" ON "GameLoans"("UserId", "GameId");

-- 6. Tabla: UserGameReviews (Micro-reseñas y Votaciones)
CREATE TABLE IF NOT EXISTS "UserGameReviews" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId" text NOT NULL,
    "GameId" uuid NOT NULL REFERENCES "Games"("Id") ON DELETE CASCADE,
    "Rating" integer NOT NULL,
    "MicroReview" text NOT NULL,
    "PlayerCountRatings" jsonb NOT NULL DEFAULT '[]',
    "FamilyExperience" jsonb,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "UpdatedAt" timestamp with time zone
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserGameReviews_UserId_GameId" ON "UserGameReviews"("UserId", "GameId");
CREATE INDEX IF NOT EXISTS "IX_UserGameReviews_GameId" ON "UserGameReviews"("GameId");

-- 7. Tabla: FoundingVerdicts (Veredictos de la Mesa Fundadora)
CREATE TABLE IF NOT EXISTS "FoundingVerdicts" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "GameId" uuid NOT NULL REFERENCES "Games"("Id") ON DELETE CASCADE,
    "Badge" integer NOT NULL,
    "CoupleSummary" text NOT NULL,
    "FamilySummary" text NOT NULL,
    "VerdictSummary" text NOT NULL,
    "Photos" jsonb NOT NULL DEFAULT '[]',
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "UpdatedAt" timestamp with time zone
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_FoundingVerdicts_GameId" ON "FoundingVerdicts"("GameId");

-- 8. Tabla: MediaItems (Hub Multimedia de YouTube e Instagram)
CREATE TABLE IF NOT EXISTS "MediaItems" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "GameId" uuid REFERENCES "Games"("Id") ON DELETE SET NULL,
    "Title" text NOT NULL,
    "Url" text NOT NULL,
    "Type" integer NOT NULL,
    "Category" integer NOT NULL,
    "Platform" integer NOT NULL,
    "ChannelName" text NOT NULL,
    "ThumbnailUrl" text,
    "DurationSeconds" integer,
    "Status" integer NOT NULL DEFAULT 0,
    "PublishedAt" timestamp with time zone NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now())
);

CREATE INDEX IF NOT EXISTS "IX_MediaItems_GameId" ON "MediaItems"("GameId");
CREATE INDEX IF NOT EXISTS "IX_MediaItems_Status" ON "MediaItems"("Status");
CREATE INDEX IF NOT EXISTS "IX_MediaItems_Type" ON "MediaItems"("Type");
CREATE INDEX IF NOT EXISTS "IX_MediaItems_Category" ON "MediaItems"("Category");
CREATE INDEX IF NOT EXISTS "IX_MediaItems_Platform" ON "MediaItems"("Platform");
CREATE INDEX IF NOT EXISTS "IX_MediaItems_GameId_Status" ON "MediaItems"("GameId", "Status");

-- 9. Tabla: Giveaways (Radar de Sorteos)
CREATE TABLE IF NOT EXISTS "Giveaways" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "Title" text NOT NULL,
    "Organizer" text NOT NULL,
    "Platform" integer NOT NULL,
    "PostUrl" text NOT NULL,
    "DeadlineAt" timestamp with time zone NOT NULL,
    "Requirements" text NOT NULL,
    "Country" varchar(100) NOT NULL DEFAULT 'España',
    "IsCommunityExclusive" boolean NOT NULL DEFAULT false,
    "IsPromoted" boolean NOT NULL DEFAULT false,
    "GameId" uuid REFERENCES "Games"("Id") ON DELETE SET NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now())
);

CREATE INDEX IF NOT EXISTS "IX_Giveaways_DeadlineAt" ON "Giveaways"("DeadlineAt");
CREATE INDEX IF NOT EXISTS "IX_Giveaways_Platform" ON "Giveaways"("Platform");
CREATE INDEX IF NOT EXISTS "IX_Giveaways_IsCommunityExclusive" ON "Giveaways"("IsCommunityExclusive");
CREATE INDEX IF NOT EXISTS "IX_Giveaways_Country" ON "Giveaways"("Country");

-- 10. Tabla: WeeklyReleases (Novedades Semanales de Viernes)
CREATE TABLE IF NOT EXISTS "WeeklyReleases" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "Title" text NOT NULL,
    "Editorial" text NOT NULL,
    "ReleaseDate" date NOT NULL,
    "Synopsis" text NOT NULL,
    "CoverUrl" text,
    "Country" varchar(100) NOT NULL DEFAULT 'España',
    "GameId" uuid REFERENCES "Games"("Id") ON DELETE SET NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now())
);

CREATE INDEX IF NOT EXISTS "IX_WeeklyReleases_ReleaseDate" ON "WeeklyReleases"("ReleaseDate");
CREATE INDEX IF NOT EXISTS "IX_WeeklyReleases_Country" ON "WeeklyReleases"("Country");

-- 11. Tablas de Reglas Q&A (RuleQuestions, RuleAnswers, RuleVotes)
CREATE TABLE IF NOT EXISTS "RuleQuestions" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "GameId" uuid NOT NULL REFERENCES "Games"("Id") ON DELETE CASCADE,
    "UserId" text NOT NULL,
    "Title" text NOT NULL,
    "Body" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "IsResolved" boolean NOT NULL DEFAULT false
);

CREATE INDEX IF NOT EXISTS "IX_RuleQuestions_GameId" ON "RuleQuestions"("GameId");
CREATE INDEX IF NOT EXISTS "IX_RuleQuestions_UserId" ON "RuleQuestions"("UserId");

CREATE TABLE IF NOT EXISTS "RuleAnswers" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "QuestionId" uuid NOT NULL REFERENCES "RuleQuestions"("Id") ON DELETE CASCADE,
    "UserId" text NOT NULL,
    "Body" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "IsOfficial" boolean NOT NULL DEFAULT false,
    "OfficialSource" text
);

CREATE INDEX IF NOT EXISTS "IX_RuleAnswers_QuestionId" ON "RuleAnswers"("QuestionId");

CREATE TABLE IF NOT EXISTS "RuleVotes" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "AnswerId" uuid NOT NULL REFERENCES "RuleAnswers"("Id") ON DELETE CASCADE,
    "UserId" text NOT NULL,
    "IsUpvote" boolean NOT NULL,
    "VotedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now())
);

CREATE INDEX IF NOT EXISTS "IX_RuleVotes_AnswerId" ON "RuleVotes"("AnswerId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_RuleVotes_AnswerId_UserId" ON "RuleVotes"("AnswerId", "UserId");

-- 12. Tablas de Expansiones y Mezclador (ExpansionSynergies, ExpansionRecipes)
CREATE TABLE IF NOT EXISTS "ExpansionSynergies" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "BaseGameId" uuid NOT NULL REFERENCES "Games"("Id") ON DELETE CASCADE,
    "Expansion1Id" uuid NOT NULL REFERENCES "Games"("Id") ON DELETE CASCADE,
    "Expansion2Id" uuid NOT NULL REFERENCES "Games"("Id") ON DELETE CASCADE,
    "SynergyScore" integer NOT NULL,
    "ComplexityDelta" integer NOT NULL,
    "Verdict" text NOT NULL,
    "RecommendedPlayerCount" integer
);

CREATE INDEX IF NOT EXISTS "IX_ExpansionSynergies_BaseGameId" ON "ExpansionSynergies"("BaseGameId");

CREATE TABLE IF NOT EXISTS "ExpansionRecipes" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "BaseGameId" uuid NOT NULL REFERENCES "Games"("Id") ON DELETE CASCADE,
    "Name" text NOT NULL,
    "TargetAudience" integer NOT NULL,
    "Description" text NOT NULL,
    "ExpansionIds" text[] NOT NULL DEFAULT '{}',
    "EstimatedDurationMinutes" integer NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_ExpansionRecipes_BaseGameId" ON "ExpansionRecipes"("BaseGameId");

-- 13. Tabla: CommunityNotificationLogs (Logs de Webhooks)
CREATE TABLE IF NOT EXISTS "CommunityNotificationLogs" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "Channel" integer NOT NULL,
    "Target" text NOT NULL,
    "Title" text NOT NULL,
    "Message" text NOT NULL,
    "SentAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "Success" boolean NOT NULL,
    "ErrorMessage" text
);

CREATE INDEX IF NOT EXISTS "IX_CommunityNotificationLogs_SentAt" ON "CommunityNotificationLogs"("SentAt");

-- 14. Tabla: UserPreferences (Preferencias de Usuario)
CREATE TABLE IF NOT EXISTS "UserPreferences" (
    "UserId" text PRIMARY KEY,
    "PreferredCountry" varchar(100),
    "Theme" varchar(50),
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "UpdatedAt" timestamp with time zone
);

-- 15. Tabla: GameIssueReports (Reportes Comunitarios y Moderación)
CREATE TABLE IF NOT EXISTS "GameIssueReports" (
    "Id" text PRIMARY KEY,
    "GameId" uuid NOT NULL REFERENCES "Games"("Id") ON DELETE CASCADE,
    "UserId" text NOT NULL,
    "Type" integer NOT NULL,
    "Details" text NOT NULL,
    "Status" integer NOT NULL DEFAULT 0,
    "ModeratorNotes" text,
    "ReportedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "ResolvedAt" timestamp with time zone
);

CREATE INDEX IF NOT EXISTS "IX_GameIssueReports_GameId" ON "GameIssueReports"("GameId");
CREATE INDEX IF NOT EXISTS "IX_GameIssueReports_Status" ON "GameIssueReports"("Status");

-- 16. Tabla: GameEditLogs (Auditoría Editorial de Juegos)
CREATE TABLE IF NOT EXISTS "GameEditLogs" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "GameId" uuid NOT NULL REFERENCES "Games"("Id") ON DELETE CASCADE,
    "UserId" text NOT NULL,
    "UserName" text NOT NULL,
    "EditedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "Summary" text NOT NULL,
    "FieldChanges" jsonb NOT NULL DEFAULT '[]'
);

CREATE INDEX IF NOT EXISTS "IX_GameEditLogs_GameId" ON "GameEditLogs"("GameId");

-- 17. Tabla: Publishers (Directorio de Editoriales)
CREATE TABLE IF NOT EXISTS "Publishers" (
    "Id" text PRIMARY KEY,
    "Name" text NOT NULL,
    "Country" text,
    "WebsiteUrl" text,
    "LogoUrl" text,
    "CatalogCount" integer NOT NULL DEFAULT 0,
    "YouTubeChannelUrl" text,
    "InstagramUrl" text,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now())
);

-- 18. Tabla: Creators (Directorio de Creadores de Contenido)
CREATE TABLE IF NOT EXISTS "Creators" (
    "Id" text PRIMARY KEY,
    "Name" text NOT NULL,
    "ChannelName" text NOT NULL,
    "Country" text,
    "Platform" integer NOT NULL DEFAULT 0,
    "ProfileUrl" text NOT NULL,
    "AvatarUrl" text,
    "Bio" text,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now())
);

-- 19. Tabla: Stores (Directorio de Tiendas Especializadas)
CREATE TABLE IF NOT EXISTS "Stores" (
    "Id" text PRIMARY KEY,
    "Name" text NOT NULL,
    "Domain" text NOT NULL,
    "Country" text,
    "LogoUrl" text,
    "AffiliateCode" text,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now())
);

-- 20. Tabla: AppUsers (Usuarios y Roles RBAC)
CREATE TABLE IF NOT EXISTS "AppUsers" (
    "Id" text PRIMARY KEY,
    "UserName" text NOT NULL,
    "Email" text NOT NULL,
    "Country" varchar(100),
    "Role" integer NOT NULL DEFAULT 0,
    "Status" integer NOT NULL DEFAULT 0,
    "Permissions" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "UpdatedAt" timestamp with time zone
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppUsers_Email" ON "AppUsers"("Email");

-- 21. Tabla: AuditLogs (Bitácora Inmutable de Auditoría)
CREATE TABLE IF NOT EXISTS "AuditLogs" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId" text NOT NULL,
    "UserName" text NOT NULL,
    "Action" integer NOT NULL,
    "EntityType" integer NOT NULL,
    "EntityId" text NOT NULL,
    "EntityName" text NOT NULL,
    "Summary" text NOT NULL,
    "Changes" jsonb NOT NULL DEFAULT '[]',
    "Timestamp" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now())
);

CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Timestamp" ON "AuditLogs"("Timestamp");
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_UserId" ON "AuditLogs"("UserId");

-- 22. Tabla: BoardGameEvents (Grandes Eventos y Ferias Lúdicas)
CREATE TABLE IF NOT EXISTS "BoardGameEvents" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "Title" text NOT NULL,
    "Organizer" text NOT NULL,
    "Location" text NOT NULL,
    "City" text NOT NULL,
    "Country" varchar(100) NOT NULL DEFAULT 'España',
    "StartDate" date NOT NULL,
    "EndDate" date NOT NULL,
    "OfficialUrl" text NOT NULL,
    "PosterUrl" text,
    "Description" text NOT NULL,
    "Status" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now())
);

CREATE INDEX IF NOT EXISTS "IX_BoardGameEvents_StartDate" ON "BoardGameEvents"("StartDate");
CREATE INDEX IF NOT EXISTS "IX_BoardGameEvents_Country" ON "BoardGameEvents"("Country");

-- 23. Tabla: NightlyCatalogingExecutionLogs (Cola Nocturna BGG/Gemini)
CREATE TABLE IF NOT EXISTS "NightlyCatalogingExecutionLogs" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "StartedAt" timestamp with time zone NOT NULL,
    "FinishedAt" timestamp with time zone,
    "DiscoveredFromNewsCount" integer NOT NULL DEFAULT 0,
    "ProcessedBggCount" integer NOT NULL DEFAULT 0,
    "SummarizedGeminiCount" integer NOT NULL DEFAULT 0,
    "ErrorsCount" integer NOT NULL DEFAULT 0,
    "Status" integer NOT NULL DEFAULT 0,
    "Details" text NOT NULL DEFAULT ''
);

CREATE INDEX IF NOT EXISTS "IX_NightlyCatalogingExecutionLogs_StartedAt" ON "NightlyCatalogingExecutionLogs"("StartedAt");

-- 24. Tabla: InstagramPostDrafts (Publicador Directo de Instagram)
CREATE TABLE IF NOT EXISTS "InstagramPostDrafts" (
    "Id" uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    "SourceType" integer NOT NULL,
    "SourceId" text NOT NULL,
    "Caption" text NOT NULL,
    "Hashtags" text[] NOT NULL DEFAULT '{}',
    "CardImageUrl" text NOT NULL,
    "CardStyle" integer NOT NULL DEFAULT 0,
    "Status" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamp with time zone NOT NULL DEFAULT timezone('utc'::text, now()),
    "PublishedAt" timestamp with time zone,
    "InstagramPostId" text,
    "ErrorMessage" text
);

CREATE INDEX IF NOT EXISTS "IX_InstagramPostDrafts_Status" ON "InstagramPostDrafts"("Status");
CREATE INDEX IF NOT EXISTS "IX_InstagramPostDrafts_CreatedAt" ON "InstagramPostDrafts"("CreatedAt");

-- ------------------------------------------------------------------------------
-- Semillado Garantizado: Usuario Administrador Fundador Permanente
-- (Se crea únicamente si no existe ningún usuario en la tabla AppUsers)
-- ------------------------------------------------------------------------------
INSERT INTO "AppUsers" ("Id", "UserName", "Email", "Country", "Role", "Status", "Permissions", "CreatedAt")
SELECT 
    'admin-fundador', 
    'Administrador Ludeka', 
    'admin@ludeka.es', 
    'España', 
    2, -- UserRole.FoundingTeam
    0, -- UserStatus.Active
    127, -- ModeratorPermission.All
    timezone('utc'::text, now())
WHERE NOT EXISTS (
    SELECT 1 FROM "AppUsers" WHERE "Role" = 2
);
