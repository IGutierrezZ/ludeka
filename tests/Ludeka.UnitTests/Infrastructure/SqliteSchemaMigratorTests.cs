using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteSchemaMigratorTests
{
    [Fact]
    public async Task EnsureSchemaUpToDateAsync_ConTablaGamesAntigua_DebeAgregarColumnasYTablasFaltantes()
    {
        // Arrange: Crear una base de datos SQLite en memoria con el esquema antiguo (sin BaseGameId ni tablas de expansiones)
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        // Crear tabla Games antigua
        using (var createCmd = connection.CreateCommand())
        {
            createCmd.CommandText = """
                CREATE TABLE "Games" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Games" PRIMARY KEY,
                    "BggId" INTEGER NOT NULL,
                    "Slug" TEXT NOT NULL,
                    "OriginalTitle" TEXT NOT NULL,
                    "SpanishTitle" TEXT NOT NULL,
                    "Designer" TEXT NOT NULL,
                    "Publisher" TEXT NOT NULL,
                    "YearPublished" INTEGER NOT NULL,
                    "CoverImageUrl" TEXT NULL,
                    "ThumbnailUrl" TEXT NULL,
                    "Description" TEXT NULL,
                    "BggRating" REAL NOT NULL,
                    "BggRank" INTEGER NULL,
                    "LudistRating" REAL NOT NULL,
                    "Confrontation" INTEGER NOT NULL,
                    "Style" INTEGER NOT NULL,
                    "IsOfficialSolo" INTEGER NOT NULL,
                    "Language" INTEGER NOT NULL,
                    "Footprint" INTEGER NOT NULL,
                    "Scalability" TEXT NOT NULL,
                    "Sleeves" TEXT NOT NULL,
                    "Age_BoxAge" INTEGER NOT NULL,
                    "Age_CommunityAge" INTEGER NOT NULL,
                    "Duration_EstimatedPerPlayerMinutes" INTEGER NOT NULL,
                    "Duration_MaxMinutes" INTEGER NOT NULL,
                    "Duration_MinMinutes" INTEGER NOT NULL
                );
                """;
            await createCmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new LudekaDbContext(options);

        // Act: Ejecutar la reconciliación defensiva de esquema
        await SqliteSchemaMigrator.EnsureSchemaUpToDateAsync(db);

        // Assert: Ahora consultar db.Games no debe arrojar 'no such column: g.BaseGameId'
        var games = await db.Games.ToListAsync();
        Assert.Empty(games);

        // Comprobar que las tablas de expansiones y notificaciones ahora existen
        var synergies = await db.ExpansionSynergies.ToListAsync();
        Assert.Empty(synergies);

        var recipes = await db.ExpansionRecipes.ToListAsync();
        Assert.Empty(recipes);

        var notificationLogs = await db.NotificationLogs.ToListAsync();
        Assert.Empty(notificationLogs);
    }

    [Fact]
    public async Task EnsureSchemaUpToDateAsync_ConTablaExternalLoginsPreexistenteDeInc46_DebeReconciliarProviderEmailVerifiedAt()
    {
        // Arrange: base SQLite con el esquema exacto de INC-46 (6 columnas, sin ProviderEmailVerifiedAt)
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using (var createCmd = connection.CreateCommand())
        {
            // "Games" debe existir para que el migrador considere la base "preexistente" y no se
            // limite a delegar en EnsureCreated (mismo umbral que usa el resto de este fichero).
            createCmd.CommandText = """
                CREATE TABLE "Games" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Games" PRIMARY KEY,
                    "BggId" INTEGER NOT NULL,
                    "Slug" TEXT NOT NULL,
                    "OriginalTitle" TEXT NOT NULL,
                    "SpanishTitle" TEXT NOT NULL,
                    "Designer" TEXT NOT NULL,
                    "Publisher" TEXT NOT NULL,
                    "YearPublished" INTEGER NOT NULL,
                    "Description" TEXT NULL,
                    "BggRating" REAL NOT NULL,
                    "LudistRating" REAL NOT NULL,
                    "Confrontation" INTEGER NOT NULL,
                    "Style" INTEGER NOT NULL,
                    "IsOfficialSolo" INTEGER NOT NULL,
                    "Language" INTEGER NOT NULL,
                    "Footprint" INTEGER NOT NULL,
                    "Scalability" TEXT NOT NULL,
                    "Sleeves" TEXT NOT NULL,
                    "Age_BoxAge" INTEGER NOT NULL,
                    "Age_CommunityAge" INTEGER NOT NULL,
                    "Duration_EstimatedPerPlayerMinutes" INTEGER NOT NULL,
                    "Duration_MaxMinutes" INTEGER NOT NULL,
                    "Duration_MinMinutes" INTEGER NOT NULL
                );
                CREATE TABLE "AppUsers" (
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
                CREATE TABLE "ExternalLogins" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_ExternalLogins" PRIMARY KEY,
                    "UserId" TEXT NOT NULL,
                    "Provider" TEXT NOT NULL,
                    "ProviderKey" TEXT NOT NULL,
                    "ProviderEmail" TEXT NULL,
                    "LinkedAt" TEXT NOT NULL,
                    CONSTRAINT "FK_ExternalLogins_AppUsers_UserId" FOREIGN KEY ("UserId") REFERENCES "AppUsers" ("Id") ON DELETE CASCADE
                );
                """;
            await createCmd.ExecuteNonQueryAsync();
        }

        using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.CommandText = """
                INSERT INTO "AppUsers" ("Id", "UserName", "Email", "Role", "Status", "Permissions", "CreatedAt")
                VALUES ('user-legacy', 'Usuario Legacy', 'legacy@ludeka.es', 0, 0, 0, '2026-09-01T00:00:00+00:00');
                INSERT INTO "ExternalLogins" ("Id", "UserId", "Provider", "ProviderKey", "LinkedAt")
                VALUES ('11111111-1111-1111-1111-111111111111', 'user-legacy', 'Google', 'google-legacy', '2026-09-01T00:00:00+00:00');
                """;
            await insertCmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(connection)
            .Options;
        using var db = new LudekaDbContext(options);

        // Act
        await SqliteSchemaMigrator.EnsureSchemaUpToDateAsync(db);

        // Assert (1): PRAGMA table_info ya incluye la columna nueva
        var columns = new List<string>();
        using (var pragmaCmd = connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA table_info('ExternalLogins');";
            using var reader = await pragmaCmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                columns.Add(reader.GetString(1));
            }
        }
        Assert.Contains("ProviderEmailVerifiedAt", columns);

        // Assert (2): la fila preexistente no se pierde, trunca ni elimina
        using (var countCmd = connection.CreateCommand())
        {
            countCmd.CommandText = "SELECT COUNT(*) FROM \"ExternalLogins\" WHERE \"Id\" = '11111111-1111-1111-1111-111111111111';";
            var count = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
            Assert.Equal(1, count);
        }

        // Assert (3): el modelo completo consulta la tabla reconciliada sin "no such column"
        var logins = await db.ExternalLogins.AsNoTracking().ToListAsync();
        var legacyLogin = Assert.Single(logins);
        Assert.Null(legacyLogin.ProviderEmailVerifiedAt);
    }
}
