using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace Ludeka.Infrastructure.Data;

/// <summary>
/// Proporciona reconciliación y actualización defensiva del esquema SQLite para bases de datos existentes.
/// Garantiza compatibilidad retroactiva cuando se introducen nuevas columnas o tablas sin requerir migraciones manuales.
/// </summary>
public static class SqliteSchemaMigrator
{
    public static async Task EnsureSchemaUpToDateAsync(LudekaDbContext db, CancellationToken ct = default)
    {
        var connection = db.Database.GetDbConnection();
        bool shouldClose = false;

        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
            shouldClose = true;
        }

        try
        {
            // 1. Obtener tablas existentes en SQLite
            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var tablesCmd = connection.CreateCommand())
            {
                tablesCmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
                using var reader = await tablesCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    existingTables.Add(reader.GetString(0));
                }
            }

            // Si la base de datos no tiene la tabla Games, EnsureCreated se encargará de crearla completa
            if (!existingTables.Contains("Games"))
            {
                return;
            }

            // 2. Reconciliar columnas de la tabla Games (Incremento 8: Expansiones y Ecosistema)
            var existingColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var columnsCmd = connection.CreateCommand())
            {
                columnsCmd.CommandText = "PRAGMA table_info('Games');";
                using var reader = await columnsCmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                {
                    existingColumns.Add(reader.GetString(1)); // nombre de la columna
                }
            }

            var columnsToAdd = new (string Name, string TypeSql)[]
            {
                ("BaseGameId", "TEXT NULL"),
                ("Type", "INTEGER NOT NULL DEFAULT 0"),
                ("ExpansionNecessity", "INTEGER NULL"),
                ("ImpactTags", "TEXT NOT NULL DEFAULT '[]'"),
                ("WhatItBringsSummary", "TEXT NULL"),
                ("ExtraPlayerCount", "INTEGER NULL"),
                ("ExtraDurationMinutes", "INTEGER NULL"),
                ("PurchaseLinks", "TEXT NOT NULL DEFAULT '[]'"),
                ("AiSummary", "TEXT NULL"),
                ("BackCoverImageUrl", "TEXT NULL"),
                ("TableImageUrl", "TEXT NULL")
            };

            foreach (var (colName, colDef) in columnsToAdd)
            {
                if (!existingColumns.Contains(colName))
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = $"ALTER TABLE \"Games\" ADD COLUMN \"{colName}\" {colDef};";
                    await alterCmd.ExecuteNonQueryAsync(ct);
                }
            }

            // Asegurar que las filas existentes no tengan valores NULL en colecciones JSON o enumerados no nulos
            using (var fixCmd = connection.CreateCommand())
            {
                fixCmd.CommandText = """
                    UPDATE "Games" SET "ImpactTags" = '[]' WHERE "ImpactTags" IS NULL;
                    UPDATE "Games" SET "PurchaseLinks" = '[]' WHERE "PurchaseLinks" IS NULL;
                    UPDATE "Games" SET "Type" = 0 WHERE "Type" IS NULL;
                    """;
                await fixCmd.ExecuteNonQueryAsync(ct);
            }

            // Crear índices en Games si no existen
            using (var idxCmd = connection.CreateCommand())
            {
                idxCmd.CommandText = """
                    CREATE INDEX IF NOT EXISTS "IX_Games_BaseGameId" ON "Games" ("BaseGameId");
                    CREATE INDEX IF NOT EXISTS "IX_Games_Type" ON "Games" ("Type");
                    """;
                await idxCmd.ExecuteNonQueryAsync(ct);
            }

            // 3. Crear tabla ExpansionSynergies si no existe (Incremento 8)
            if (!existingTables.Contains("ExpansionSynergies"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "ExpansionSynergies" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_ExpansionSynergies" PRIMARY KEY,
                        "BaseGameId" TEXT NOT NULL,
                        "ExpansionAId" TEXT NOT NULL,
                        "ExpansionBId" TEXT NOT NULL,
                        "Level" INTEGER NOT NULL,
                        "Reason" TEXT NOT NULL,
                        CONSTRAINT "FK_ExpansionSynergies_Games_BaseGameId" FOREIGN KEY ("BaseGameId") REFERENCES "Games" ("Id") ON DELETE CASCADE,
                        CONSTRAINT "FK_ExpansionSynergies_Games_ExpansionAId" FOREIGN KEY ("ExpansionAId") REFERENCES "Games" ("Id") ON DELETE CASCADE,
                        CONSTRAINT "FK_ExpansionSynergies_Games_ExpansionBId" FOREIGN KEY ("ExpansionBId") REFERENCES "Games" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS "IX_ExpansionSynergies_BaseGameId" ON "ExpansionSynergies" ("BaseGameId");
                    CREATE INDEX IF NOT EXISTS "IX_ExpansionSynergies_ExpansionAId_ExpansionBId" ON "ExpansionSynergies" ("ExpansionAId", "ExpansionBId");
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("ExpansionSynergies");
            }

            // 4. Crear tabla ExpansionRecipes si no existe (Incremento 8)
            if (!existingTables.Contains("ExpansionRecipes"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "ExpansionRecipes" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_ExpansionRecipes" PRIMARY KEY,
                        "BaseGameId" TEXT NOT NULL,
                        "Name" TEXT NOT NULL,
                        "Description" TEXT NOT NULL,
                        "IdealFor" TEXT NOT NULL,
                        "IncludedExpansionIds" TEXT NOT NULL DEFAULT '[]',
                        CONSTRAINT "FK_ExpansionRecipes_Games_BaseGameId" FOREIGN KEY ("BaseGameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS "IX_ExpansionRecipes_BaseGameId" ON "ExpansionRecipes" ("BaseGameId");
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("ExpansionRecipes");
            }

            // 5. Crear tabla NotificationLogs si no existe (Incremento 9)
            if (!existingTables.Contains("NotificationLogs"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "NotificationLogs" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_NotificationLogs" PRIMARY KEY,
                        "EventType" INTEGER NOT NULL,
                        "Channel" INTEGER NOT NULL,
                        "Title" TEXT NOT NULL,
                        "Summary" TEXT NOT NULL,
                        "TargetUrl" TEXT NULL,
                        "ImageUrl" TEXT NULL,
                        "Status" INTEGER NOT NULL,
                        "ErrorDetails" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "SentAt" TEXT NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_NotificationLogs_Status" ON "NotificationLogs" ("Status");
                    CREATE INDEX IF NOT EXISTS "IX_NotificationLogs_Channel" ON "NotificationLogs" ("Channel");
                    CREATE INDEX IF NOT EXISTS "IX_NotificationLogs_CreatedAt" ON "NotificationLogs" ("CreatedAt");
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("NotificationLogs");
            }

            // 6. Crear tabla UserPreferences si no existe
            if (!existingTables.Contains("UserPreferences"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "UserPreferences" (
                        "UserId" TEXT NOT NULL CONSTRAINT "PK_UserPreferences" PRIMARY KEY,
                        "PreferredTheme" TEXT NOT NULL,
                        "Country" TEXT NULL,
                        "UpdatedAt" TEXT NOT NULL
                    );
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("UserPreferences");
            }

            // 6.1 Crear tabla GameIssueReports si no existe (Incremento 17)
            if (!existingTables.Contains("GameIssueReports"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "GameIssueReports" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_GameIssueReports" PRIMARY KEY,
                        "GameId" TEXT NOT NULL,
                        "GameSlug" TEXT NOT NULL,
                        "GameTitle" TEXT NOT NULL,
                        "IssueType" INTEGER NOT NULL,
                        "Details" TEXT NULL,
                        "ReportedByUserId" TEXT NULL,
                        "ReporterNameOrAlias" TEXT NOT NULL,
                        "Status" INTEGER NOT NULL,
                        "ModeratorNotes" TEXT NULL,
                        "ResolvedByUserId" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL,
                        "ResolvedAt" TEXT NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_GameIssueReports_GameId" ON "GameIssueReports" ("GameId");
                    CREATE INDEX IF NOT EXISTS "IX_GameIssueReports_Status" ON "GameIssueReports" ("Status");
                    CREATE INDEX IF NOT EXISTS "IX_GameIssueReports_IssueType" ON "GameIssueReports" ("IssueType");
                    CREATE INDEX IF NOT EXISTS "IX_GameIssueReports_CreatedAt" ON "GameIssueReports" ("CreatedAt");
                    CREATE INDEX IF NOT EXISTS "IX_GameIssueReports_Status_CreatedAt" ON "GameIssueReports" ("Status", "CreatedAt");
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("GameIssueReports");
            }

            // 7. Crear tabla GameEditLogs si no existe (Incremento 18)
            if (!existingTables.Contains("GameEditLogs"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "GameEditLogs" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_GameEditLogs" PRIMARY KEY,
                        "GameId" TEXT NOT NULL,
                        "EditorUserId" TEXT NOT NULL,
                        "EditorName" TEXT NOT NULL,
                        "SummaryOfChanges" TEXT NOT NULL,
                        "AssociatedReportId" TEXT NULL,
                        "EditedAt" TEXT NOT NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_GameEditLogs_GameId" ON "GameEditLogs" ("GameId");
                    CREATE INDEX IF NOT EXISTS "IX_GameEditLogs_EditedAt" ON "GameEditLogs" ("EditedAt");
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("GameEditLogs");
            }

            // 8. Crear tabla Publishers si no existe (Incremento 19)
            if (!existingTables.Contains("Publishers"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "Publishers" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_Publishers" PRIMARY KEY,
                        "Name" TEXT NOT NULL,
                        "Slug" TEXT NOT NULL,
                        "Country" TEXT NOT NULL,
                        "City" TEXT NULL,
                        "Description" TEXT NULL,
                        "LogoUrl" TEXT NULL,
                        "WebsiteUrl" TEXT NULL,
                        "SocialLinks" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Publishers_Slug" ON "Publishers" ("Slug");
                    CREATE INDEX IF NOT EXISTS "IX_Publishers_Name" ON "Publishers" ("Name");
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("Publishers");
            }

            // 9. Crear tabla Creators si no existe (Incremento 19)
            if (!existingTables.Contains("Creators"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "Creators" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_Creators" PRIMARY KEY,
                        "Name" TEXT NOT NULL,
                        "Slug" TEXT NOT NULL,
                        "Nationality" TEXT NULL,
                        "Bio" TEXT NULL,
                        "AvatarUrl" TEXT NULL,
                        "BggPersonId" INTEGER NULL,
                        "WebsiteUrl" TEXT NULL,
                        "SocialLinks" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Creators_Slug" ON "Creators" ("Slug");
                    CREATE INDEX IF NOT EXISTS "IX_Creators_Name" ON "Creators" ("Name");
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("Creators");
            }

            // 10. Crear tabla Stores si no existe (Incremento 19 y 29)
            if (!existingTables.Contains("Stores"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "Stores" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_Stores" PRIMARY KEY,
                        "Name" TEXT NOT NULL,
                        "Slug" TEXT NOT NULL,
                        "Type" INTEGER NOT NULL,
                        "Country" TEXT NOT NULL DEFAULT 'España',
                        "ShippingCountries" TEXT NULL,
                        "City" TEXT NULL,
                        "Address" TEXT NULL,
                        "Description" TEXT NULL,
                        "LogoUrl" TEXT NULL,
                        "WebsiteUrl" TEXT NULL,
                        "AffiliateCode" TEXT NULL,
                        "HasLoyaltyProgram" INTEGER NOT NULL,
                        "SocialLinks" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_Stores_Slug" ON "Stores" ("Slug");
                    CREATE INDEX IF NOT EXISTS "IX_Stores_Name" ON "Stores" ("Name");
                    CREATE INDEX IF NOT EXISTS "IX_Stores_Country" ON "Stores" ("Country");
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("Stores");
            }

            // 11. Crear tabla AppUsers si no existe (Incremento 20 y 29)
            if (!existingTables.Contains("AppUsers"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "AppUsers" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_AppUsers" PRIMARY KEY,
                        "UserName" TEXT NOT NULL,
                        "Email" TEXT NOT NULL,
                        "Role" INTEGER NOT NULL,
                        "Status" INTEGER NOT NULL,
                        "Country" TEXT NULL,
                        "Permissions" INTEGER NOT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_AppUsers_Email" ON "AppUsers" ("Email");
                    CREATE INDEX IF NOT EXISTS "IX_AppUsers_Role" ON "AppUsers" ("Role");
                    CREATE INDEX IF NOT EXISTS "IX_AppUsers_Status" ON "AppUsers" ("Status");
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("AppUsers");
            }

            // 12. Crear tabla AuditLogs si no existe (Incremento 20)
            if (!existingTables.Contains("AuditLogs"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "AuditLogs" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_AuditLogs" PRIMARY KEY,
                        "UserId" TEXT NOT NULL,
                        "UserName" TEXT NOT NULL,
                        "Timestamp" TEXT NOT NULL,
                        "Action" INTEGER NOT NULL,
                        "EntityType" INTEGER NOT NULL,
                        "EntityId" TEXT NOT NULL,
                        "EntityName" TEXT NOT NULL,
                        "Summary" TEXT NOT NULL,
                        "Changes" TEXT NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_AuditLogs_UserId" ON "AuditLogs" ("UserId");
                    CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Timestamp" ON "AuditLogs" ("Timestamp");
                    CREATE INDEX IF NOT EXISTS "IX_AuditLogs_EntityType" ON "AuditLogs" ("EntityType");
                    CREATE INDEX IF NOT EXISTS "IX_AuditLogs_Action" ON "AuditLogs" ("Action");
                    CREATE INDEX IF NOT EXISTS "IX_AuditLogs_EntityType_EntityId" ON "AuditLogs" ("EntityType", "EntityId");
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("AuditLogs");
            }

            // 13. Reconciliar columna IsPromoted en tabla Giveaways (Incremento 21)
            if (existingTables.Contains("Giveaways"))
            {
                var giveawayColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var colsCmd = connection.CreateCommand())
                {
                    colsCmd.CommandText = "PRAGMA table_info('Giveaways');";
                    using var reader = await colsCmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        giveawayColumns.Add(reader.GetString(1));
                    }
                }

                if (!giveawayColumns.Contains("IsPromoted"))
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE \"Giveaways\" ADD COLUMN \"IsPromoted\" INTEGER NOT NULL DEFAULT 0;";
                    await alterCmd.ExecuteNonQueryAsync(ct);
                }
            }

            // 14. Crear tabla BoardGameEvents si no existe (Incremento 21 y 29)
            if (!existingTables.Contains("BoardGameEvents"))
            {
                using var createCmd = connection.CreateCommand();
                createCmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "BoardGameEvents" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_BoardGameEvents" PRIMARY KEY,
                        "Title" TEXT NOT NULL,
                        "Description" TEXT NOT NULL,
                        "Country" TEXT NOT NULL DEFAULT 'España',
                        "ImageUrl" TEXT NOT NULL,
                        "StartDate" TEXT NOT NULL,
                        "EndDate" TEXT NOT NULL,
                        "Location" TEXT NOT NULL,
                        "WebsiteUrl" TEXT NULL,
                        "Organizer" TEXT NOT NULL,
                        "IsOfficial" INTEGER NOT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_BoardGameEvents_StartDate" ON "BoardGameEvents" ("StartDate");
                    CREATE INDEX IF NOT EXISTS "IX_BoardGameEvents_IsOfficial" ON "BoardGameEvents" ("IsOfficial");
                    CREATE INDEX IF NOT EXISTS "IX_BoardGameEvents_Country" ON "BoardGameEvents" ("Country");
                    """;
                await createCmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("BoardGameEvents");
            }

            // 15. Reconciliar columna Category en tabla MediaItems (Incremento 23)
            if (existingTables.Contains("MediaItems"))
            {
                var mediaColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var colsCmd = connection.CreateCommand())
                {
                    colsCmd.CommandText = "PRAGMA table_info('MediaItems');";
                    using var reader = await colsCmd.ExecuteReaderAsync(ct);
                    while (await reader.ReadAsync(ct))
                    {
                        mediaColumns.Add(reader.GetString(1));
                    }
                }

                if (!mediaColumns.Contains("Category"))
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE \"MediaItems\" ADD COLUMN \"Category\" INTEGER NOT NULL DEFAULT 0;";
                    await alterCmd.ExecuteNonQueryAsync(ct);
                }

                using var idxCmd = connection.CreateCommand();
                idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS \"IX_MediaItems_Category\" ON \"MediaItems\" (\"Category\");";
                await idxCmd.ExecuteNonQueryAsync(ct);
            }

            // 16. Reconciliar Country y ShippingCountries en Stores, Giveaways, BoardGameEvents, AppUsers, UserPreferences (Incremento 29)
            if (existingTables.Contains("Stores"))
            {
                var cols = await GetTableColumnsAsync(connection, "Stores", ct);
                if (!cols.Contains("Country"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"Stores\" ADD COLUMN \"Country\" TEXT NOT NULL DEFAULT 'España';";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                if (!cols.Contains("ShippingCountries"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"Stores\" ADD COLUMN \"ShippingCountries\" TEXT NULL;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                using var idxCmd = connection.CreateCommand();
                idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS \"IX_Stores_Country\" ON \"Stores\" (\"Country\");";
                await idxCmd.ExecuteNonQueryAsync(ct);
            }

            if (existingTables.Contains("Giveaways"))
            {
                var cols = await GetTableColumnsAsync(connection, "Giveaways", ct);
                if (!cols.Contains("Country"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"Giveaways\" ADD COLUMN \"Country\" TEXT NOT NULL DEFAULT 'España';";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                using var idxCmd = connection.CreateCommand();
                idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS \"IX_Giveaways_Country\" ON \"Giveaways\" (\"Country\");";
                await idxCmd.ExecuteNonQueryAsync(ct);
            }

            if (existingTables.Contains("BoardGameEvents"))
            {
                var cols = await GetTableColumnsAsync(connection, "BoardGameEvents", ct);
                if (!cols.Contains("Country"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"BoardGameEvents\" ADD COLUMN \"Country\" TEXT NOT NULL DEFAULT 'España';";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                using var idxCmd = connection.CreateCommand();
                idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS \"IX_BoardGameEvents_Country\" ON \"BoardGameEvents\" (\"Country\");";
                await idxCmd.ExecuteNonQueryAsync(ct);
            }

            if (existingTables.Contains("AppUsers"))
            {
                var cols = await GetTableColumnsAsync(connection, "AppUsers", ct);
                if (!cols.Contains("Country"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"AppUsers\" ADD COLUMN \"Country\" TEXT NULL;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }

            if (existingTables.Contains("UserPreferences"))
            {
                var cols = await GetTableColumnsAsync(connection, "UserPreferences", ct);
                if (!cols.Contains("Country"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"UserPreferences\" ADD COLUMN \"Country\" TEXT NULL;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }

            // --- Incremento 24: Detección Automática y Cola Nocturna Inteligente ---
            if (existingTables.Contains("PendingBggImports"))
            {
                var cols = await GetTableColumnsAsync(connection, "PendingBggImports", ct);
                if (!cols.Contains("Origin"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"PendingBggImports\" ADD COLUMN \"Origin\" INTEGER NOT NULL DEFAULT 0;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                if (!cols.Contains("ExtractedTitle"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"PendingBggImports\" ADD COLUMN \"ExtractedTitle\" TEXT NULL;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }

            if (!existingTables.Contains("NightlyCatalogingExecutionLogs"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "NightlyCatalogingExecutionLogs" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_NightlyCatalogingExecutionLogs" PRIMARY KEY,
                        "StartedAt" TEXT NOT NULL,
                        "CompletedAt" TEXT NULL,
                        "QueueProcessedCount" INTEGER NOT NULL,
                        "NewsDiscoveryCount" INTEGER NOT NULL,
                        "TopBackfillCount" INTEGER NOT NULL,
                        "TotalCatalogedCount" INTEGER NOT NULL,
                        "FailedCount" INTEGER NOT NULL,
                        "CatalogedTitlesJson" TEXT NOT NULL,
                        "Status" TEXT NOT NULL,
                        "ErrorMessage" TEXT NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_NightlyCatalogingExecutionLogs_StartedAt" ON "NightlyCatalogingExecutionLogs" ("StartedAt");
                """;
                await cmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("NightlyCatalogingExecutionLogs");
            }

            // Reconciliar columnas de Giveaways para Instagram (Incremento 28)
            if (existingTables.Contains("Giveaways"))
            {
                var giveawayCols = await GetTableColumnsAsync(connection, "Giveaways", ct);
                if (!giveawayCols.Contains("InstagramMediaId"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"Giveaways\" ADD COLUMN \"InstagramMediaId\" TEXT NULL;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                if (!giveawayCols.Contains("InstagramPermalink"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"Giveaways\" ADD COLUMN \"InstagramPermalink\" TEXT NULL;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }

            // Reconciliar columnas de WeeklyReleases para Instagram (Incremento 28)
            if (existingTables.Contains("WeeklyReleases"))
            {
                var releaseCols = await GetTableColumnsAsync(connection, "WeeklyReleases", ct);
                if (!releaseCols.Contains("InstagramMediaId"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"WeeklyReleases\" ADD COLUMN \"InstagramMediaId\" TEXT NULL;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                if (!releaseCols.Contains("InstagramPermalink"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"WeeklyReleases\" ADD COLUMN \"InstagramPermalink\" TEXT NULL;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                if (!releaseCols.Contains("UpdatedAt"))
                {
                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = "ALTER TABLE \"WeeklyReleases\" ADD COLUMN \"UpdatedAt\" TEXT NULL;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }

            // Crear tabla InstagramPostDrafts si no existe (Incremento 28)
            if (!existingTables.Contains("InstagramPostDrafts"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "InstagramPostDrafts" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_InstagramPostDrafts" PRIMARY KEY,
                        "SourceType" INTEGER NOT NULL,
                        "SourceId" TEXT NOT NULL,
                        "Title" TEXT NOT NULL,
                        "Caption" TEXT NOT NULL,
                        "ImageUrl" TEXT NULL,
                        "SvgContent" TEXT NULL,
                        "Theme" TEXT NOT NULL,
                        "Status" INTEGER NOT NULL,
                        "InstagramMediaId" TEXT NULL,
                        "InstagramPermalink" TEXT NULL,
                        "ErrorMessage" TEXT NULL,
                        "CreatedByUserId" TEXT NOT NULL,
                        "CreatedByUserName" TEXT NOT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "PublishedAt" TEXT NULL,
                        "UpdatedAt" TEXT NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_InstagramPostDrafts_Status" ON "InstagramPostDrafts" ("Status");
                    CREATE INDEX IF NOT EXISTS "IX_InstagramPostDrafts_SourceType_SourceId" ON "InstagramPostDrafts" ("SourceType", "SourceId");
                    CREATE INDEX IF NOT EXISTS "IX_InstagramPostDrafts_CreatedAt" ON "InstagramPostDrafts" ("CreatedAt");
                """;
                await cmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("InstagramPostDrafts");
            }

            // --- Incremento 30: Desacoplamiento de IsPlayed, Fusión de Wishlist y Diario de Partidas ---
            if (existingTables.Contains("UserCollectionItems"))
            {
                var collectionCols = await GetTableColumnsAsync(connection, "UserCollectionItems", ct);
                if (!collectionCols.Contains("IsPlayed"))
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = "ALTER TABLE \"UserCollectionItems\" ADD COLUMN \"IsPlayed\" INTEGER NOT NULL DEFAULT 0;";
                    await alterCmd.ExecuteNonQueryAsync(ct);

                    // Migrar registros existentes con Status = 2 (Played) a IsPlayed = 1 y Status = NULL
                    using var migratePlayedCmd = connection.CreateCommand();
                    migratePlayedCmd.CommandText = "UPDATE \"UserCollectionItems\" SET \"IsPlayed\" = 1, \"Status\" = NULL WHERE \"Status\" = 2;";
                    await migratePlayedCmd.ExecuteNonQueryAsync(ct);
                }

                // Migrar registros históricos de Wishlist (3) a WantToBuy (4)
                using var migrateWishlistCmd = connection.CreateCommand();
                migrateWishlistCmd.CommandText = "UPDATE \"UserCollectionItems\" SET \"Status\" = 4 WHERE \"Status\" = 3;";
                await migrateWishlistCmd.ExecuteNonQueryAsync(ct);

                using var idxCmd = connection.CreateCommand();
                idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS \"IX_UserCollectionItems_UserId_IsPlayed\" ON \"UserCollectionItems\" (\"UserId\", \"IsPlayed\");";
                await idxCmd.ExecuteNonQueryAsync(ct);
            }

            // Crear tabla GamePlayLogs si no existe (Incremento 30)
            if (!existingTables.Contains("GamePlayLogs"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "GamePlayLogs" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_GamePlayLogs" PRIMARY KEY,
                        "UserId" TEXT NOT NULL,
                        "GameId" TEXT NOT NULL,
                        "PlayDate" TEXT NOT NULL,
                        "Location" TEXT NOT NULL,
                        "PlayerCount" INTEGER NOT NULL,
                        "DurationMinutes" INTEGER NULL,
                        "Comment" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        CONSTRAINT "FK_GamePlayLogs_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS "IX_GamePlayLogs_UserId_PlayDate" ON "GamePlayLogs" ("UserId", "PlayDate");
                    CREATE INDEX IF NOT EXISTS "IX_GamePlayLogs_GameId" ON "GamePlayLogs" ("GameId");
                """;
                await cmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("GamePlayLogs");
            }

            // Crear tabla BggCatalogStaging si no existe (Incremento 41)
            if (!existingTables.Contains("BggCatalogStaging"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "BggCatalogStaging" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_BggCatalogStaging" PRIMARY KEY,
                        "BggId" INTEGER NOT NULL,
                        "Title" TEXT NOT NULL,
                        "YearPublished" INTEGER NULL,
                        "BggRank" INTEGER NULL,
                        "UsersRated" INTEGER NOT NULL,
                        "BayesAverage" REAL NOT NULL,
                        "AverageRating" REAL NOT NULL,
                        "SpanishTitle" TEXT NULL,
                        "Designer" TEXT NULL,
                        "Publisher" TEXT NULL,
                        "Description" TEXT NULL,
                        "MinPlayers" INTEGER NOT NULL DEFAULT 1,
                        "MaxPlayers" INTEGER NOT NULL DEFAULT 4,
                        "PlayingTimeMinutes" INTEGER NOT NULL DEFAULT 60,
                        "MinAge" INTEGER NOT NULL DEFAULT 10,
                        "Weight" REAL NOT NULL DEFAULT 2.5,
                        "FetchStatus" INTEGER NOT NULL DEFAULT 0,
                        "RawBggXml" TEXT NULL,
                        "FetchError" TEXT NULL,
                        "FetchedAt" TEXT NULL,
                        "ImagesStatus" INTEGER NOT NULL DEFAULT 0,
                        "CoverImageUrl" TEXT NULL,
                        "ThumbnailUrl" TEXT NULL,
                        "BackCoverImageUrl" TEXT NULL,
                        "TableImageUrl" TEXT NULL,
                        "ImagesError" TEXT NULL,
                        "ImagesProcessedAt" TEXT NULL,
                        "AiStatus" INTEGER NOT NULL DEFAULT 0,
                        "AiSummaryJson" TEXT NULL,
                        "AiError" TEXT NULL,
                        "AiProcessedAt" TEXT NULL,
                        "PromotionStatus" INTEGER NOT NULL DEFAULT 0,
                        "PromotionError" TEXT NULL,
                        "PromotedAt" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NOT NULL
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_BggCatalogStaging_BggId" ON "BggCatalogStaging" ("BggId");
                    CREATE INDEX IF NOT EXISTS "IX_BggCatalogStaging_FetchStatus_UsersRated" ON "BggCatalogStaging" ("FetchStatus", "UsersRated");
                    CREATE INDEX IF NOT EXISTS "IX_BggCatalogStaging_ImagesStatus_UsersRated" ON "BggCatalogStaging" ("ImagesStatus", "UsersRated");
                    CREATE INDEX IF NOT EXISTS "IX_BggCatalogStaging_AiStatus_UsersRated" ON "BggCatalogStaging" ("AiStatus", "UsersRated");
                    CREATE INDEX IF NOT EXISTS "IX_BggCatalogStaging_PromotionStatus_UsersRated" ON "BggCatalogStaging" ("PromotionStatus", "UsersRated");
                """;
                await cmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("BggCatalogStaging");
            }

            // 17. Reconciliar tabla SocialInboxItems (Incremento 42)
            if (!existingTables.Contains("SocialInboxItems"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "SocialInboxItems" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_SocialInboxItems" PRIMARY KEY,
                        "SourceUrl" TEXT NOT NULL,
                        "Platform" INTEGER NOT NULL,
                        "DetectedType" INTEGER NOT NULL,
                        "Status" INTEGER NOT NULL,
                        "Title" TEXT NOT NULL,
                        "OrganizerOrAuthor" TEXT NOT NULL,
                        "Collaborator" TEXT NULL,
                        "GameId" TEXT NULL,
                        "GameTitle" TEXT NULL,
                        "EventOrReleaseDate" TEXT NULL,
                        "EventEndDate" TEXT NULL,
                        "Location" TEXT NULL,
                        "EstimatedPvp" TEXT NULL,
                        "MediaCategory" INTEGER NULL,
                        "PlayerCountBadge" TEXT NULL,
                        "OriginalCaption" TEXT NULL,
                        "ThumbnailUrl" TEXT NULL,
                        "IsVideo" INTEGER NOT NULL,
                        "AiAnalysisNotes" TEXT NULL,
                        "CreatedEntityId" TEXT NULL,
                        "ModeratorNotes" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "ReviewedAt" TEXT NULL,
                        "ReviewedByUserId" TEXT NULL,
                        CONSTRAINT "FK_SocialInboxItems_Games_GameId" FOREIGN KEY ("GameId") REFERENCES "Games" ("Id") ON DELETE SET NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_SocialInboxItems_Status" ON "SocialInboxItems" ("Status");
                    CREATE INDEX IF NOT EXISTS "IX_SocialInboxItems_DetectedType" ON "SocialInboxItems" ("DetectedType");
                    CREATE INDEX IF NOT EXISTS "IX_SocialInboxItems_CreatedAt" ON "SocialInboxItems" ("CreatedAt");
                    CREATE INDEX IF NOT EXISTS "IX_SocialInboxItems_GameId" ON "SocialInboxItems" ("GameId");
                """;
                await cmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("SocialInboxItems");
            }

            // 18. Reconciliar tabla MonitoredSocialAccounts (Incremento 42)
            if (!existingTables.Contains("MonitoredSocialAccounts"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "MonitoredSocialAccounts" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_MonitoredSocialAccounts" PRIMARY KEY,
                        "Name" TEXT NOT NULL,
                        "Platform" INTEGER NOT NULL,
                        "HandleOrChannelId" TEXT NOT NULL,
                        "AccountType" INTEGER NOT NULL,
                        "ProfileUrl" TEXT NOT NULL,
                        "IsEnabled" INTEGER NOT NULL,
                        "LastCheckedAt" TEXT NULL,
                        "ResolvedFeedUrl" TEXT NULL,
                        "Notes" TEXT NULL,
                        "CreatedAt" TEXT NOT NULL,
                        "UpdatedAt" TEXT NULL
                    );
                    CREATE INDEX IF NOT EXISTS "IX_MonitoredSocialAccounts_Platform_HandleOrChannelId" ON "MonitoredSocialAccounts" ("Platform", "HandleOrChannelId");
                    CREATE INDEX IF NOT EXISTS "IX_MonitoredSocialAccounts_IsEnabled" ON "MonitoredSocialAccounts" ("IsEnabled");
                """;
                await cmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("MonitoredSocialAccounts");
            }
            else
            {
                var accountCols = await GetTableColumnsAsync(connection, "MonitoredSocialAccounts", ct);
                if (!accountCols.Contains("ResolvedFeedUrl"))
                {
                    using var addCol = connection.CreateCommand();
                    addCol.CommandText = "ALTER TABLE \"MonitoredSocialAccounts\" ADD COLUMN \"ResolvedFeedUrl\" TEXT NULL;";
                    await addCol.ExecuteNonQueryAsync(ct);
                }
            }

            // 21. Tabla GamePriceSnapshots (Incremento 45: Radar de Bajadas de Precios y Mínimos Históricos)
            if (!existingTables.Contains("GamePriceSnapshots"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "GamePriceSnapshots" (
                        "Id" TEXT PRIMARY KEY,
                        "GameId" TEXT NOT NULL,
                        "StoreName" TEXT NOT NULL,
                        "AffiliateUrl" TEXT NOT NULL,
                        "Price" REAL NOT NULL,
                        "Currency" TEXT NOT NULL,
                        "InStock" INTEGER NOT NULL,
                        "RecordedAtUtc" TEXT NOT NULL,
                        FOREIGN KEY ("GameId") REFERENCES "Games"("Id") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS "IX_GamePriceSnapshots_GameId_RecordedAtUtc" ON "GamePriceSnapshots" ("GameId", "RecordedAtUtc" DESC);
                    CREATE INDEX IF NOT EXISTS "IX_GamePriceSnapshots_Price" ON "GamePriceSnapshots" ("Price");
                    CREATE INDEX IF NOT EXISTS "IX_GamePriceSnapshots_GameId_StoreName" ON "GamePriceSnapshots" ("GameId", "StoreName");
                """;
                await cmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("GamePriceSnapshots");
            }

            // 22. Reconciliar tabla ExternalLogins (Incremento 46: Autenticación real e identidad externa)
            if (!existingTables.Contains("ExternalLogins"))
            {
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    CREATE TABLE IF NOT EXISTS "ExternalLogins" (
                        "Id" TEXT NOT NULL CONSTRAINT "PK_ExternalLogins" PRIMARY KEY,
                        "UserId" TEXT NOT NULL,
                        "Provider" TEXT NOT NULL,
                        "ProviderKey" TEXT NOT NULL,
                        "ProviderEmail" TEXT NULL,
                        "ProviderEmailVerifiedAt" TEXT NULL,
                        "LinkedAt" TEXT NOT NULL,
                        CONSTRAINT "FK_ExternalLogins_AppUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AppUsers" ("Id") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS "IX_ExternalLogins_Provider_ProviderKey" ON "ExternalLogins" ("Provider", "ProviderKey");
                    CREATE INDEX IF NOT EXISTS "IX_ExternalLogins_UserId" ON "ExternalLogins" ("UserId");
                    """;
                await cmd.ExecuteNonQueryAsync(ct);
                existingTables.Add("ExternalLogins");
            }

            // 23. Reconciliar columna ProviderEmailVerifiedAt en ExternalLogins (Incremento 49: Vinculación de cuentas)
            if (existingTables.Contains("ExternalLogins"))
            {
                var externalLoginColumns = await GetTableColumnsAsync(connection, "ExternalLogins", ct);
                if (!externalLoginColumns.Contains("ProviderEmailVerifiedAt"))
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText =
                        "ALTER TABLE \"ExternalLogins\" ADD COLUMN \"ProviderEmailVerifiedAt\" TEXT NULL;";
                    await alterCmd.ExecuteNonQueryAsync(ct);
                }
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<HashSet<string>> GetTableColumnsAsync(System.Data.Common.DbConnection connection, string tableName, CancellationToken ct)
    {
        var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info('{tableName}');";
        using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            cols.Add(reader.GetString(1));
        }
        return cols;
    }
}
