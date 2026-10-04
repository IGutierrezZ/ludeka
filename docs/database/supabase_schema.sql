CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "AppUsers" (
        "Id" character varying(100) NOT NULL,
        "UserName" character varying(150) NOT NULL,
        "Email" character varying(200) NOT NULL,
        "Country" character varying(100),
        "Role" integer NOT NULL,
        "Status" integer NOT NULL,
        "Permissions" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AppUsers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "AuditLogs" (
        "Id" uuid NOT NULL,
        "UserId" character varying(100) NOT NULL,
        "UserName" character varying(150) NOT NULL,
        "Timestamp" timestamp with time zone NOT NULL,
        "Action" integer NOT NULL,
        "EntityType" integer NOT NULL,
        "EntityId" character varying(100) NOT NULL,
        "EntityName" character varying(200) NOT NULL,
        "Summary" character varying(500) NOT NULL,
        "Changes" jsonb,
        CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "BoardGameEvents" (
        "Id" uuid NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Description" text NOT NULL,
        "ImageUrl" character varying(500) NOT NULL,
        "StartDate" date NOT NULL,
        "EndDate" date NOT NULL,
        "Country" character varying(100) NOT NULL,
        "Location" character varying(200) NOT NULL,
        "WebsiteUrl" text,
        "Organizer" text NOT NULL,
        "IsOfficial" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_BoardGameEvents" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "Creators" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Slug" character varying(200) NOT NULL,
        "Nationality" text,
        "Bio" text,
        "AvatarUrl" text,
        "BggPersonId" integer,
        "WebsiteUrl" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "SocialLinks" jsonb,
        CONSTRAINT "PK_Creators" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "FoundingVerdicts" (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "AuthorUserId" text NOT NULL,
        "AuthorName" text NOT NULL,
        "Recommendation" integer NOT NULL,
        "OverallVerdict" text NOT NULL,
        "TwoPlayerVerdict" text NOT NULL,
        "FamilyVerdict" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "Photos" jsonb,
        CONSTRAINT "PK_FoundingVerdicts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "GameEditLogs" (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "EditorUserId" character varying(100) NOT NULL,
        "EditorName" character varying(100) NOT NULL,
        "SummaryOfChanges" character varying(1000) NOT NULL,
        "AssociatedReportId" uuid,
        "EditedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_GameEditLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "GameIssueReports" (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "GameSlug" character varying(200) NOT NULL,
        "GameTitle" character varying(250) NOT NULL,
        "IssueType" integer NOT NULL,
        "Details" character varying(1000),
        "ReportedByUserId" character varying(100),
        "ReporterNameOrAlias" character varying(100) NOT NULL,
        "Status" integer NOT NULL,
        "ModeratorNotes" character varying(1000),
        "ResolvedByUserId" character varying(100),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "ResolvedAt" timestamp with time zone,
        CONSTRAINT "PK_GameIssueReports" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "Games" (
        "Id" uuid NOT NULL,
        "BggId" integer NOT NULL,
        "Slug" text NOT NULL,
        "OriginalTitle" text NOT NULL,
        "SpanishTitle" text NOT NULL,
        "Designer" text NOT NULL,
        "Publisher" text NOT NULL,
        "YearPublished" integer NOT NULL,
        "CoverImageUrl" text,
        "ThumbnailUrl" text,
        "Description" text,
        "BggRating" double precision NOT NULL,
        "BggRank" integer,
        "LudistRating" double precision NOT NULL,
        "Confrontation" integer NOT NULL,
        "Style" integer NOT NULL,
        "IsOfficialSolo" boolean NOT NULL,
        "Language" integer NOT NULL,
        "Footprint" integer NOT NULL,
        "Type" integer NOT NULL,
        "BaseGameId" uuid,
        "ExpansionNecessity" integer,
        "ImpactTags" integer[] NOT NULL,
        "WhatItBringsSummary" text,
        "ExtraPlayerCount" integer,
        "ExtraDurationMinutes" integer,
        "Age_BoxAge" integer NOT NULL,
        "Age_CommunityAge" integer NOT NULL,
        "Duration_EstimatedPerPlayerMinutes" integer NOT NULL,
        "Duration_MaxMinutes" integer NOT NULL,
        "Duration_MinMinutes" integer NOT NULL,
        "AiSummary" jsonb,
        "PurchaseLinks" jsonb,
        "Scalability" jsonb,
        "Sleeves" jsonb,
        CONSTRAINT "PK_Games" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Games_Games_BaseGameId" FOREIGN KEY ("BaseGameId") REFERENCES "Games" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "InstagramPostDrafts" (
        "Id" uuid NOT NULL,
        "SourceType" integer NOT NULL,
        "SourceId" text NOT NULL,
        "Title" character varying(250) NOT NULL,
        "Caption" character varying(4000) NOT NULL,
        "ImageUrl" text,
        "SvgContent" text,
        "Theme" character varying(20) NOT NULL,
        "Status" integer NOT NULL,
        "InstagramMediaId" text,
        "InstagramPermalink" text,
        "ErrorMessage" text,
        "CreatedByUserId" character varying(100) NOT NULL,
        "CreatedByUserName" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "PublishedAt" timestamp with time zone,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_InstagramPostDrafts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "NightlyCatalogingExecutionLogs" (
        "Id" uuid NOT NULL,
        "StartedAt" timestamp with time zone NOT NULL,
        "CompletedAt" timestamp with time zone,
        "QueueProcessedCount" integer NOT NULL,
        "NewsDiscoveryCount" integer NOT NULL,
        "TopBackfillCount" integer NOT NULL,
        "TotalCatalogedCount" integer NOT NULL,
        "FailedCount" integer NOT NULL,
        "CatalogedTitlesJson" text NOT NULL,
        "Status" character varying(50) NOT NULL,
        "ErrorMessage" text,
        CONSTRAINT "PK_NightlyCatalogingExecutionLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "NotificationLogs" (
        "Id" uuid NOT NULL,
        "EventType" integer NOT NULL,
        "Channel" integer NOT NULL,
        "Title" text NOT NULL,
        "Summary" text NOT NULL,
        "TargetUrl" text,
        "ImageUrl" text,
        "Status" integer NOT NULL,
        "ErrorDetails" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "SentAt" timestamp with time zone,
        CONSTRAINT "PK_NotificationLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "PendingBggImports" (
        "Id" uuid NOT NULL,
        "BggId" integer NOT NULL,
        "Title" text NOT NULL,
        "YearPublished" integer,
        "ThumbnailUrl" text,
        "CoverImageUrl" text,
        "Origin" integer NOT NULL,
        "ExtractedTitle" text,
        "RequestedCount" integer NOT NULL,
        "Status" integer NOT NULL,
        "ErrorMessage" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ProcessedAt" timestamp with time zone,
        CONSTRAINT "PK_PendingBggImports" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "Publishers" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Slug" character varying(200) NOT NULL,
        "Country" text NOT NULL,
        "City" text,
        "Description" text,
        "LogoUrl" text,
        "WebsiteUrl" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "SocialLinks" jsonb,
        CONSTRAINT "PK_Publishers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "RuleVotes" (
        "Id" uuid NOT NULL,
        "UserId" text NOT NULL,
        "QuestionId" uuid,
        "AnswerId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_RuleVotes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "Stores" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Slug" character varying(200) NOT NULL,
        "Type" integer NOT NULL,
        "Country" character varying(100) NOT NULL,
        "City" text,
        "Address" text,
        "Description" text,
        "LogoUrl" text,
        "WebsiteUrl" text,
        "AffiliateCode" text,
        "HasLoyaltyProgram" boolean NOT NULL,
        "ShippingCountries" text[] NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "SocialLinks" jsonb,
        CONSTRAINT "PK_Stores" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "UserPreferences" (
        "UserId" text NOT NULL,
        "PreferredTheme" character varying(32) NOT NULL,
        "Country" character varying(100),
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_UserPreferences" PRIMARY KEY ("UserId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "ExpansionRecipes" (
        "Id" uuid NOT NULL,
        "BaseGameId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Description" text NOT NULL,
        "IdealFor" text NOT NULL,
        "IncludedExpansionIds" uuid[] NOT NULL,
        CONSTRAINT "PK_ExpansionRecipes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ExpansionRecipes_Games_BaseGameId" FOREIGN KEY ("BaseGameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "ExpansionSynergies" (
        "Id" uuid NOT NULL,
        "BaseGameId" uuid NOT NULL,
        "ExpansionAId" uuid NOT NULL,
        "ExpansionBId" uuid NOT NULL,
        "Level" integer NOT NULL,
        "Reason" text NOT NULL,
        CONSTRAINT "PK_ExpansionSynergies" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ExpansionSynergies_Games_BaseGameId" FOREIGN KEY ("BaseGameId") REFERENCES "Games" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ExpansionSynergies_Games_ExpansionAId" FOREIGN KEY ("ExpansionAId") REFERENCES "Games" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ExpansionSynergies_Games_ExpansionBId" FOREIGN KEY ("ExpansionBId") REFERENCES "Games" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "GameLoans" (
        "Id" uuid NOT NULL,
        "UserId" text NOT NULL,
        "GameId" uuid NOT NULL,
        "BorrowerName" text NOT NULL,
        "LoanDate" timestamp with time zone NOT NULL,
        "Notes" text,
        "IsReturned" boolean NOT NULL,
        "ReturnedDate" timestamp with time zone,
        CONSTRAINT "PK_GameLoans" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_GameLoans_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "GamePlayLogs" (
        "Id" uuid NOT NULL,
        "UserId" character varying(100) NOT NULL,
        "GameId" uuid NOT NULL,
        "PlayDate" timestamp with time zone NOT NULL,
        "Location" character varying(150) NOT NULL,
        "PlayerCount" integer NOT NULL,
        "DurationMinutes" integer,
        "Comment" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_GamePlayLogs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_GamePlayLogs_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "Giveaways" (
        "Id" uuid NOT NULL,
        "Title" text NOT NULL,
        "Organizer" text NOT NULL,
        "Collaborator" text,
        "Url" text NOT NULL,
        "Platform" integer NOT NULL,
        "Country" character varying(100) NOT NULL,
        "DeadlineAt" timestamp with time zone NOT NULL,
        "GameId" uuid,
        "GameTitle" text,
        "ThumbnailUrl" text,
        "IsCommunityExclusive" boolean NOT NULL,
        "IsPromoted" boolean NOT NULL,
        "InstagramMediaId" text,
        "InstagramPermalink" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Giveaways" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Giveaways_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "MediaItems" (
        "Id" uuid NOT NULL,
        "GameId" uuid,
        "Type" integer NOT NULL,
        "Category" integer NOT NULL,
        "Platform" integer NOT NULL,
        "Title" text NOT NULL,
        "Url" text NOT NULL,
        "EmbedUrl" text,
        "ThumbnailUrl" text NOT NULL,
        "AuthorChannel" text NOT NULL,
        "DurationSeconds" integer,
        "PlayerCountBadge" text,
        "LikesCount" integer,
        "Excerpt" text,
        "Status" integer NOT NULL,
        "IsBroken" boolean NOT NULL,
        "PublishedAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_MediaItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_MediaItems_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "RuleQuestions" (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "UserId" text NOT NULL,
        "UserName" text NOT NULL,
        "Title" text NOT NULL,
        "Body" text NOT NULL,
        "VotesCount" integer NOT NULL,
        "AcceptedAnswerId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_RuleQuestions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RuleQuestions_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "UserCollectionItems" (
        "Id" uuid NOT NULL,
        "UserId" text NOT NULL,
        "GameId" uuid,
        "Status" integer,
        "IsPlayed" boolean NOT NULL DEFAULT FALSE,
        "AddedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "BggId" integer,
        "PendingTitle" text,
        "PendingThumbnailUrl" text,
        "PendingYearPublished" integer,
        CONSTRAINT "PK_UserCollectionItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserCollectionItems_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "UserGameReviews" (
        "Id" uuid NOT NULL,
        "UserId" text NOT NULL,
        "GameId" uuid NOT NULL,
        "Score" double precision NOT NULL,
        "MicroReview" text,
        "PlayContext" integer,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "FamilyExperience" jsonb,
        "PlayerCountRatings" jsonb,
        CONSTRAINT "PK_UserGameReviews" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserGameReviews_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "WeeklyReleases" (
        "Id" uuid NOT NULL,
        "Title" text NOT NULL,
        "Publisher" text NOT NULL,
        "ReleaseDate" date NOT NULL,
        "GameId" uuid,
        "CoverImageUrl" text,
        "EstimatedPvp" numeric,
        "IsReprint" boolean NOT NULL,
        "Notes" text,
        "InstagramMediaId" text,
        "InstagramPermalink" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_WeeklyReleases" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WeeklyReleases_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE TABLE "RuleAnswers" (
        "Id" uuid NOT NULL,
        "QuestionId" uuid NOT NULL,
        "UserId" text NOT NULL,
        "UserName" text NOT NULL,
        "Body" text NOT NULL,
        "OfficialRuleReference" text,
        "VotesCount" integer NOT NULL,
        "IsAccepted" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_RuleAnswers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RuleAnswers_RuleQuestions_QuestionId" FOREIGN KEY ("QuestionId") REFERENCES "RuleQuestions" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE UNIQUE INDEX "IX_AppUsers_Email" ON "AppUsers" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_AppUsers_Role" ON "AppUsers" ("Role");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_AppUsers_Status" ON "AppUsers" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_AuditLogs_Action" ON "AuditLogs" ("Action");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_AuditLogs_EntityType" ON "AuditLogs" ("EntityType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_AuditLogs_EntityType_EntityId" ON "AuditLogs" ("EntityType", "EntityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_AuditLogs_Timestamp" ON "AuditLogs" ("Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_AuditLogs_UserId" ON "AuditLogs" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_BoardGameEvents_Country" ON "BoardGameEvents" ("Country");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_BoardGameEvents_IsOfficial" ON "BoardGameEvents" ("IsOfficial");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_BoardGameEvents_StartDate" ON "BoardGameEvents" ("StartDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Creators_Name" ON "Creators" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE UNIQUE INDEX "IX_Creators_Slug" ON "Creators" ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_ExpansionRecipes_BaseGameId" ON "ExpansionRecipes" ("BaseGameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_ExpansionSynergies_BaseGameId" ON "ExpansionSynergies" ("BaseGameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_ExpansionSynergies_ExpansionAId_ExpansionBId" ON "ExpansionSynergies" ("ExpansionAId", "ExpansionBId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_ExpansionSynergies_ExpansionBId" ON "ExpansionSynergies" ("ExpansionBId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE UNIQUE INDEX "IX_FoundingVerdicts_GameId" ON "FoundingVerdicts" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GameEditLogs_EditedAt" ON "GameEditLogs" ("EditedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GameEditLogs_GameId" ON "GameEditLogs" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GameIssueReports_CreatedAt" ON "GameIssueReports" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GameIssueReports_GameId" ON "GameIssueReports" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GameIssueReports_IssueType" ON "GameIssueReports" ("IssueType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GameIssueReports_Status" ON "GameIssueReports" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GameIssueReports_Status_CreatedAt" ON "GameIssueReports" ("Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GameLoans_GameId" ON "GameLoans" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GameLoans_UserId_GameId" ON "GameLoans" ("UserId", "GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GameLoans_UserId_IsReturned" ON "GameLoans" ("UserId", "IsReturned");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GamePlayLogs_GameId" ON "GamePlayLogs" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_GamePlayLogs_UserId_PlayDate" ON "GamePlayLogs" ("UserId", "PlayDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Games_BaseGameId" ON "Games" ("BaseGameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE UNIQUE INDEX "IX_Games_BggId" ON "Games" ("BggId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Games_OriginalTitle" ON "Games" ("OriginalTitle");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE UNIQUE INDEX "IX_Games_Slug" ON "Games" ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Games_SpanishTitle" ON "Games" ("SpanishTitle");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Games_Type" ON "Games" ("Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Giveaways_Country" ON "Giveaways" ("Country");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Giveaways_DeadlineAt" ON "Giveaways" ("DeadlineAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Giveaways_GameId" ON "Giveaways" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Giveaways_IsCommunityExclusive" ON "Giveaways" ("IsCommunityExclusive");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Giveaways_Platform" ON "Giveaways" ("Platform");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_InstagramPostDrafts_CreatedAt" ON "InstagramPostDrafts" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_InstagramPostDrafts_SourceType_SourceId" ON "InstagramPostDrafts" ("SourceType", "SourceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_InstagramPostDrafts_Status" ON "InstagramPostDrafts" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_MediaItems_Category" ON "MediaItems" ("Category");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_MediaItems_GameId" ON "MediaItems" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_MediaItems_GameId_Status" ON "MediaItems" ("GameId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_MediaItems_Platform" ON "MediaItems" ("Platform");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_MediaItems_Status" ON "MediaItems" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_MediaItems_Type" ON "MediaItems" ("Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_NightlyCatalogingExecutionLogs_StartedAt" ON "NightlyCatalogingExecutionLogs" ("StartedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_NotificationLogs_Channel" ON "NotificationLogs" ("Channel");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_NotificationLogs_CreatedAt" ON "NotificationLogs" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_NotificationLogs_Status" ON "NotificationLogs" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE UNIQUE INDEX "IX_PendingBggImports_BggId" ON "PendingBggImports" ("BggId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_PendingBggImports_CreatedAt" ON "PendingBggImports" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_PendingBggImports_Status_RequestedCount" ON "PendingBggImports" ("Status", "RequestedCount");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Publishers_Name" ON "Publishers" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE UNIQUE INDEX "IX_Publishers_Slug" ON "Publishers" ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_RuleAnswers_IsAccepted" ON "RuleAnswers" ("IsAccepted");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_RuleAnswers_QuestionId" ON "RuleAnswers" ("QuestionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_RuleQuestions_CreatedAt" ON "RuleQuestions" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_RuleQuestions_GameId" ON "RuleQuestions" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_RuleVotes_UserId_AnswerId" ON "RuleVotes" ("UserId", "AnswerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_RuleVotes_UserId_QuestionId" ON "RuleVotes" ("UserId", "QuestionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Stores_Country" ON "Stores" ("Country");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_Stores_Name" ON "Stores" ("Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE UNIQUE INDEX "IX_Stores_Slug" ON "Stores" ("Slug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_UserCollectionItems_GameId" ON "UserCollectionItems" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_UserCollectionItems_UserId_BggId" ON "UserCollectionItems" ("UserId", "BggId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_UserCollectionItems_UserId_GameId" ON "UserCollectionItems" ("UserId", "GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_UserCollectionItems_UserId_IsPlayed" ON "UserCollectionItems" ("UserId", "IsPlayed");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_UserCollectionItems_UserId_Status" ON "UserCollectionItems" ("UserId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_UserGameReviews_GameId" ON "UserGameReviews" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE UNIQUE INDEX "IX_UserGameReviews_UserId_GameId" ON "UserGameReviews" ("UserId", "GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_WeeklyReleases_GameId" ON "WeeklyReleases" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    CREATE INDEX "IX_WeeklyReleases_ReleaseDate" ON "WeeklyReleases" ("ReleaseDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260914001323_InitialSupabasePostgres') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260914001323_InitialSupabasePostgres', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    ALTER TABLE "NightlyCatalogingExecutionLogs" ADD "BggDiscoveryCount" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    ALTER TABLE "Games" ADD "BackCoverImageUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    ALTER TABLE "Games" ADD "TableImageUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE TABLE "BggCatalogStaging" (
        "BggId" integer GENERATED BY DEFAULT AS IDENTITY,
        "OriginalTitle" character varying(250) NOT NULL,
        "SpanishTitle" character varying(250),
        "YearPublished" integer,
        "BggRank" integer,
        "UsersRated" integer NOT NULL,
        "BayesAverage" double precision NOT NULL,
        "AverageRating" double precision NOT NULL,
        "FetchStatus" integer NOT NULL,
        "ImagesStatus" integer NOT NULL,
        "AiStatus" integer NOT NULL,
        "PromotionStatus" integer NOT NULL,
        "RawThingXml" text,
        "Designer" character varying(200),
        "Publisher" character varying(200),
        "Description" text,
        "MinPlayers" integer NOT NULL,
        "MaxPlayers" integer NOT NULL,
        "PlayingTimeMinutes" integer NOT NULL,
        "MinAge" integer NOT NULL,
        "CoverImageUrl" text,
        "ThumbnailUrl" text,
        "BackCoverImageUrl" text,
        "TableImageUrl" text,
        "AiSummaryJson" text,
        "RetryCount" integer NOT NULL,
        "ErrorMessage" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NOT NULL,
        "ProcessedAt" timestamp with time zone,
        CONSTRAINT "PK_BggCatalogStaging" PRIMARY KEY ("BggId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE TABLE "GamePriceSnapshots" (
        "Id" uuid NOT NULL,
        "GameId" uuid NOT NULL,
        "StoreName" character varying(150) NOT NULL,
        "AffiliateUrl" character varying(500) NOT NULL,
        "Price" numeric NOT NULL,
        "Currency" character varying(10) NOT NULL,
        "InStock" boolean NOT NULL,
        "RecordedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_GamePriceSnapshots" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE TABLE "MonitoredSocialAccounts" (
        "Id" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Platform" integer NOT NULL,
        "HandleOrChannelId" character varying(150) NOT NULL,
        "AccountType" integer NOT NULL,
        "ProfileUrl" character varying(500) NOT NULL,
        "IsEnabled" boolean NOT NULL,
        "LastCheckedAt" timestamp with time zone,
        "ResolvedFeedUrl" character varying(500),
        "Notes" character varying(1000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_MonitoredSocialAccounts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE TABLE "SocialInboxItems" (
        "Id" uuid NOT NULL,
        "SourceUrl" character varying(500) NOT NULL,
        "Platform" integer NOT NULL,
        "DetectedType" integer NOT NULL,
        "Status" integer NOT NULL,
        "Title" character varying(250) NOT NULL,
        "OrganizerOrAuthor" character varying(200) NOT NULL,
        "Collaborator" character varying(200),
        "GameId" uuid,
        "GameTitle" text,
        "EventOrReleaseDate" timestamp with time zone,
        "EventEndDate" timestamp with time zone,
        "Location" character varying(200),
        "EstimatedPvp" numeric,
        "MediaCategory" integer,
        "PlayerCountBadge" character varying(50),
        "OriginalCaption" text,
        "ThumbnailUrl" character varying(1000),
        "IsVideo" boolean NOT NULL,
        "AiAnalysisNotes" text,
        "CreatedEntityId" uuid,
        "ModeratorNotes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ReviewedAt" timestamp with time zone,
        "ReviewedByUserId" text,
        CONSTRAINT "PK_SocialInboxItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_SocialInboxItems_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_BggCatalogStaging_AiStatus" ON "BggCatalogStaging" ("AiStatus");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_BggCatalogStaging_CreatedAt" ON "BggCatalogStaging" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_BggCatalogStaging_FetchStatus" ON "BggCatalogStaging" ("FetchStatus");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_BggCatalogStaging_ImagesStatus" ON "BggCatalogStaging" ("ImagesStatus");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_BggCatalogStaging_OriginalTitle" ON "BggCatalogStaging" ("OriginalTitle");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_BggCatalogStaging_PromotionStatus" ON "BggCatalogStaging" ("PromotionStatus");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_BggCatalogStaging_UsersRated" ON "BggCatalogStaging" ("UsersRated");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_GamePriceSnapshots_GameId_RecordedAtUtc" ON "GamePriceSnapshots" ("GameId", "RecordedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_GamePriceSnapshots_GameId_StoreName" ON "GamePriceSnapshots" ("GameId", "StoreName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_GamePriceSnapshots_Price" ON "GamePriceSnapshots" ("Price");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_MonitoredSocialAccounts_IsEnabled" ON "MonitoredSocialAccounts" ("IsEnabled");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_MonitoredSocialAccounts_Platform_HandleOrChannelId" ON "MonitoredSocialAccounts" ("Platform", "HandleOrChannelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_SocialInboxItems_CreatedAt" ON "SocialInboxItems" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_SocialInboxItems_DetectedType" ON "SocialInboxItems" ("DetectedType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_SocialInboxItems_GameId" ON "SocialInboxItems" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    CREATE INDEX "IX_SocialInboxItems_Status" ON "SocialInboxItems" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260915100112_AddBggStagingSocialInboxPriceRadarAndGameImages', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915164921_AddExternalLogins') THEN
    CREATE TABLE "ExternalLogins" (
        "Id" uuid NOT NULL,
        "UserId" character varying(100) NOT NULL,
        "Provider" character varying(50) NOT NULL,
        "ProviderKey" character varying(255) NOT NULL,
        "ProviderEmail" character varying(200),
        "LinkedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ExternalLogins" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ExternalLogins_AppUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AppUsers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915164921_AddExternalLogins') THEN
    CREATE UNIQUE INDEX "IX_ExternalLogins_Provider_ProviderKey" ON "ExternalLogins" ("Provider", "ProviderKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915164921_AddExternalLogins') THEN
    CREATE INDEX "IX_ExternalLogins_UserId" ON "ExternalLogins" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915164921_AddExternalLogins') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260915164921_AddExternalLogins', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917112154_AddProviderEmailVerifiedAtToExternalLogins') THEN
    ALTER TABLE "ExternalLogins" ADD "ProviderEmailVerifiedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260917112154_AddProviderEmailVerifiedAtToExternalLogins') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260917112154_AddProviderEmailVerifiedAtToExternalLogins', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919000613_AddNotificationLogDeliveryColumns') THEN
    ALTER TABLE "NotificationLogs" ADD "Attempts" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919000613_AddNotificationLogDeliveryColumns') THEN
    ALTER TABLE "NotificationLogs" ADD "MessageId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919000613_AddNotificationLogDeliveryColumns') THEN
    ALTER TABLE "NotificationLogs" ADD "NextAttemptAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919000613_AddNotificationLogDeliveryColumns') THEN
    CREATE UNIQUE INDEX "IX_NotificationLogs_MessageId_Channel" ON "NotificationLogs" ("MessageId", "Channel");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919000613_AddNotificationLogDeliveryColumns') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260919000613_AddNotificationLogDeliveryColumns', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919010426_AddNotificationOutboxMessages') THEN
    CREATE TABLE "NotificationOutboxMessages" (
        "Id" uuid NOT NULL,
        "EventType" integer NOT NULL,
        "Title" character varying(250) NOT NULL,
        "Summary" text NOT NULL,
        "TargetUrl" text,
        "ImageUrl" text,
        "FieldsJson" text NOT NULL,
        "TargetChannel" integer,
        "Status" integer NOT NULL,
        "Attempts" integer NOT NULL,
        "NextAttemptAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "CompletedAt" timestamp with time zone,
        "ClaimedAt" timestamp with time zone,
        "ClaimedBy" character varying(128),
        "LastError" text,
        CONSTRAINT "PK_NotificationOutboxMessages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919010426_AddNotificationOutboxMessages') THEN
    CREATE INDEX "IX_NotificationOutboxMessages_Status_CreatedAt" ON "NotificationOutboxMessages" ("Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919010426_AddNotificationOutboxMessages') THEN
    CREATE INDEX "IX_NotificationOutboxMessages_Status_NextAttemptAt_CreatedAt" ON "NotificationOutboxMessages" ("Status", "NextAttemptAt", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919010426_AddNotificationOutboxMessages') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260919010426_AddNotificationOutboxMessages', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919012754_AddJobExecutionLeases') THEN
    CREATE TABLE "JobExecutionLeases" (
        "Id" uuid NOT NULL,
        "JobName" character varying(64) NOT NULL,
        "WindowKey" character varying(32) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "StartedAt" timestamp with time zone NOT NULL,
        "HeartbeatAt" timestamp with time zone NOT NULL,
        "CompletedAt" timestamp with time zone,
        "ProcessedCount" integer NOT NULL,
        "FailedCount" integer NOT NULL,
        "DurationMs" bigint,
        "HostIdentifier" character varying(128),
        "ErrorMessage" text,
        CONSTRAINT "PK_JobExecutionLeases" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919012754_AddJobExecutionLeases') THEN
    CREATE INDEX "IX_JobExecutionLeases_JobName_StartedAt" ON "JobExecutionLeases" ("JobName", "StartedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919012754_AddJobExecutionLeases') THEN
    CREATE UNIQUE INDEX "IX_JobExecutionLeases_JobName_WindowKey" ON "JobExecutionLeases" ("JobName", "WindowKey");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260919012754_AddJobExecutionLeases') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260919012754_AddJobExecutionLeases', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921225042_AddDataProtectionKeys') THEN
    CREATE TABLE "DataProtectionKeys" (
        "Id" integer GENERATED BY DEFAULT AS IDENTITY,
        "FriendlyName" text,
        "Xml" text,
        CONSTRAINT "PK_DataProtectionKeys" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260921225042_AddDataProtectionKeys') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260921225042_AddDataProtectionKeys', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925134715_AddUserPreferenceProfileVisibility') THEN
    ALTER TABLE "UserPreferences" ADD "HidePublicProfile" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925134715_AddUserPreferenceProfileVisibility') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260925134715_AddUserPreferenceProfileVisibility', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925164853_AddMagicLinkTokens') THEN
    CREATE TABLE "MagicLinkTokens" (
        "Id" uuid NOT NULL,
        "Email" character varying(200) NOT NULL,
        "TokenHash" character varying(128) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "ConsumedAt" timestamp with time zone,
        "TargetUserId" character varying(100),
        CONSTRAINT "PK_MagicLinkTokens" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925164853_AddMagicLinkTokens') THEN
    CREATE INDEX "IX_MagicLinkTokens_Email_CreatedAt" ON "MagicLinkTokens" ("Email", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925164853_AddMagicLinkTokens') THEN
    CREATE UNIQUE INDEX "IX_MagicLinkTokens_TokenHash" ON "MagicLinkTokens" ("TokenHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925164853_AddMagicLinkTokens') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260925164853_AddMagicLinkTokens', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925194347_AddUserLikes') THEN
    CREATE TABLE "UserLikes" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "TargetType" integer NOT NULL,
        "TargetId" uuid NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_UserLikes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925194347_AddUserLikes') THEN
    CREATE INDEX "IX_UserLikes_TargetType_TargetId" ON "UserLikes" ("TargetType", "TargetId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925194347_AddUserLikes') THEN
    CREATE UNIQUE INDEX "IX_UserLikes_UserId_TargetType_TargetId" ON "UserLikes" ("UserId", "TargetType", "TargetId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925194347_AddUserLikes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260925194347_AddUserLikes', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925222136_AddUserMilestones') THEN
    CREATE TABLE "UserMilestones" (
        "Id" uuid NOT NULL,
        "UserId" character varying(128) NOT NULL,
        "Type" integer NOT NULL,
        "UnlockedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_UserMilestones" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925222136_AddUserMilestones') THEN
    CREATE INDEX "IX_UserMilestones_UserId" ON "UserMilestones" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925222136_AddUserMilestones') THEN
    CREATE UNIQUE INDEX "IX_UserMilestones_UserId_Type" ON "UserMilestones" ("UserId", "Type");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925222136_AddUserMilestones') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260925222136_AddUserMilestones', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925231052_AddLeaderboardPreferences') THEN
    ALTER TABLE "UserPreferences" ADD "LeaderboardAnonymous" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925231052_AddLeaderboardPreferences') THEN
    ALTER TABLE "UserPreferences" ADD "LeaderboardOptIn" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925231052_AddLeaderboardPreferences') THEN
    ALTER TABLE "UserPreferences" ADD "LeaderboardPseudonym" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925231052_AddLeaderboardPreferences') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260925231052_AddLeaderboardPreferences', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    ALTER TABLE "Games" ADD "LocalizedTitles" jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    ALTER TABLE "Games" ADD "RegionalPublishers" jsonb;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    ALTER TABLE "Games" ADD "SpanishPublisher" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    ALTER TABLE "BggCatalogStaging" ADD "InferredFootprint" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    ALTER TABLE "BggCatalogStaging" ADD "MaxPlayTimeMinutes" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    ALTER TABLE "BggCatalogStaging" ADD "MinPlayTimeMinutes" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    ALTER TABLE "BggCatalogStaging" ADD "RegionalPublishersJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    ALTER TABLE "BggCatalogStaging" ADD "ScalabilityJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    ALTER TABLE "BggCatalogStaging" ADD "SleevesJson" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    ALTER TABLE "BggCatalogStaging" ADD "SpanishPublisher" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    CREATE INDEX "IX_Games_SpanishPublisher" ON "Games" ("SpanishPublisher");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260927023336_AddBggQualityAndLocalizationFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260927023336_AddBggQualityAndLocalizationFields', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260930060047_AddWeeklyReleaseSourceUrl') THEN
    ALTER TABLE "WeeklyReleases" ADD "SourceUrl" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260930060047_AddWeeklyReleaseSourceUrl') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260930060047_AddWeeklyReleaseSourceUrl', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001115806_AddBggRawSnapshots') THEN
    CREATE TABLE "BggRawSnapshots" (
        "BggId" integer NOT NULL,
        "RawJson" jsonb NOT NULL,
        "ApiVersion" integer NOT NULL,
        "FetchedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone,
        CONSTRAINT "PK_BggRawSnapshots" PRIMARY KEY ("BggId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001115806_AddBggRawSnapshots') THEN
    CREATE INDEX "IX_BggRawSnapshots_FetchedAtUtc" ON "BggRawSnapshots" ("FetchedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001115806_AddBggRawSnapshots') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261001115806_AddBggRawSnapshots', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001131304_AddDailyTrendingGames') THEN
    CREATE TABLE "DailyTrendingGames" (
        "Id" uuid NOT NULL,
        "DateUtc" date NOT NULL,
        "Rank" integer NOT NULL,
        "BggId" integer NOT NULL,
        "Title" character varying(250) NOT NULL,
        "YearPublished" integer,
        "ThumbnailUrl" character varying(500),
        "GameId" uuid,
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_DailyTrendingGames" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DailyTrendingGames_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001131304_AddDailyTrendingGames') THEN
    CREATE INDEX "IX_DailyTrendingGames_BggId" ON "DailyTrendingGames" ("BggId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001131304_AddDailyTrendingGames') THEN
    CREATE INDEX "IX_DailyTrendingGames_DateUtc" ON "DailyTrendingGames" ("DateUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001131304_AddDailyTrendingGames') THEN
    CREATE UNIQUE INDEX "IX_DailyTrendingGames_DateUtc_BggId" ON "DailyTrendingGames" ("DateUtc", "BggId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001131304_AddDailyTrendingGames') THEN
    CREATE UNIQUE INDEX "IX_DailyTrendingGames_DateUtc_Rank" ON "DailyTrendingGames" ("DateUtc", "Rank");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001131304_AddDailyTrendingGames') THEN
    CREATE INDEX "IX_DailyTrendingGames_GameId" ON "DailyTrendingGames" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001131304_AddDailyTrendingGames') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261001131304_AddDailyTrendingGames', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001151945_MakeWeeklyReleaseDateNullable') THEN
    ALTER TABLE "WeeklyReleases" ALTER COLUMN "ReleaseDate" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001151945_MakeWeeklyReleaseDateNullable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261001151945_MakeWeeklyReleaseDateNullable', '10.0.12');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004205804_AddGameEanAndAffiliateClicks') THEN
    ALTER TABLE "Games" ADD "AdditionalBarcodes" text[] NOT NULL DEFAULT '{}';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004205804_AddGameEanAndAffiliateClicks') THEN
    ALTER TABLE "Games" ADD "Ean" character varying(14);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004205804_AddGameEanAndAffiliateClicks') THEN
    CREATE TABLE "AffiliateClicks" (
        "Id" uuid NOT NULL,
        "GameId" uuid,
        "GameTitle" character varying(250) NOT NULL,
        "GameSlug" character varying(250) NOT NULL,
        "StoreName" character varying(150) NOT NULL,
        "TargetUrl" character varying(1000) NOT NULL,
        "Country" character varying(100),
        "ClickedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AffiliateClicks" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004205804_AddGameEanAndAffiliateClicks') THEN
    CREATE INDEX "IX_Games_Ean" ON "Games" ("Ean");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004205804_AddGameEanAndAffiliateClicks') THEN
    CREATE INDEX "IX_AffiliateClicks_ClickedAtUtc" ON "AffiliateClicks" ("ClickedAtUtc");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004205804_AddGameEanAndAffiliateClicks') THEN
    CREATE INDEX "IX_AffiliateClicks_GameId" ON "AffiliateClicks" ("GameId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004205804_AddGameEanAndAffiliateClicks') THEN
    CREATE INDEX "IX_AffiliateClicks_GameSlug" ON "AffiliateClicks" ("GameSlug");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004205804_AddGameEanAndAffiliateClicks') THEN
    CREATE INDEX "IX_AffiliateClicks_StoreName" ON "AffiliateClicks" ("StoreName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004205804_AddGameEanAndAffiliateClicks') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261004205804_AddGameEanAndAffiliateClicks', '10.0.12');
    END IF;
END $EF$;
COMMIT;

