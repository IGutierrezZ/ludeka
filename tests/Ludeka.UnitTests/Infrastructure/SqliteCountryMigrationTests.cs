using System.Threading.Tasks;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class SqliteCountryMigrationTests
{
    [Fact]
    public async Task EnsureSchemaUpToDateAsync_DebeCrearColumnasCountryYShippingCountriesEnEsquemasPrevios()
    {
        // Arrange: Crear base de datos SQLite en memoria con tablas previas sin las columnas del Incremento 29
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE "Games" (
                    "Id" TEXT NOT NULL PRIMARY KEY,
                    "Slug" TEXT NOT NULL
                );

                CREATE TABLE "Stores" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Stores" PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "Slug" TEXT NOT NULL,
                    "Type" INTEGER NOT NULL,
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

                CREATE TABLE "Giveaways" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_Giveaways" PRIMARY KEY,
                    "Title" TEXT NOT NULL,
                    "Organizer" TEXT NOT NULL,
                    "Collaborator" TEXT NULL,
                    "Url" TEXT NOT NULL,
                    "Platform" INTEGER NOT NULL,
                    "DeadlineAt" TEXT NOT NULL,
                    "RemainingTimeText" TEXT NOT NULL,
                    "IsExpired" INTEGER NOT NULL,
                    "GameId" TEXT NULL,
                    "GameTitle" TEXT NULL,
                    "ThumbnailUrl" TEXT NULL,
                    "IsCommunityExclusive" INTEGER NOT NULL,
                    "IsPromoted" INTEGER NOT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "UpdatedAt" TEXT NULL
                );

                CREATE TABLE "BoardGameEvents" (
                    "Id" TEXT NOT NULL CONSTRAINT "PK_BoardGameEvents" PRIMARY KEY,
                    "Title" TEXT NOT NULL,
                    "Description" TEXT NOT NULL,
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

                CREATE TABLE "UserPreferences" (
                    "UserId" TEXT NOT NULL CONSTRAINT "PK_UserPreferences" PRIMARY KEY,
                    "PreferredTheme" TEXT NOT NULL,
                    "UpdatedAt" TEXT NOT NULL
                );
            """;
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new LudekaDbContext(options);

        // Act: Ejecutar reconciliación idempotente
        await SqliteSchemaMigrator.EnsureSchemaUpToDateAsync(db);

        // Assert: Consultar las tablas debe funcionar sin error "no such column: Country"
        var stores = await db.Stores.ToListAsync();
        Assert.Empty(stores);

        var giveaways = await db.Giveaways.ToListAsync();
        Assert.Empty(giveaways);

        var events = await db.BoardGameEvents.ToListAsync();
        Assert.Empty(events);

        var preferences = await db.UserPreferences.ToListAsync();
        Assert.Empty(preferences);

        // Comprobar que HidePublicProfile se reconcilió y se puede persistir
        db.UserPreferences.Add(new Core.Entities.UserPreference("user-priv", "wood", "España", hidePublicProfile: true));
        await db.SaveChangesAsync();

        var saved = await db.UserPreferences.FindAsync("user-priv");
        Assert.NotNull(saved);
        Assert.True(saved!.HidePublicProfile);
    }

    [Fact]
    public async Task EnsureSchemaUpToDateAsync_CuandoTablasNoExistenPreviamente_DebeCrearlasConColumnasDePais()
    {
        // Arrange: Crear base de datos SQLite en memoria que solo tiene Games (emulando una base de datos antigua que aún no tenía Stores, etc.)
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE "Games" (
                    "Id" TEXT NOT NULL PRIMARY KEY,
                    "Slug" TEXT NOT NULL
                );
            """;
            await cmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new LudekaDbContext(options);

        // Act: Ejecutar reconciliación completa
        await SqliteSchemaMigrator.EnsureSchemaUpToDateAsync(db);

        // Assert: Todas las tablas creadas dinámicamente deben incluir la columna Country y ShippingCountries
        var store = new Core.Entities.Store(
            name: "Tienda Prueba",
            slug: "tienda-prueba",
            type: Core.Enums.StoreType.OnlineOnly,
            country: "Chile",
            shippingCountries: new[] { "Chile", "Argentina" }
        );
        db.Stores.Add(store);
        await db.SaveChangesAsync();

        var storesFromDb = await db.Stores.ToListAsync();
        Assert.Single(storesFromDb);
        Assert.Equal("Chile", storesFromDb[0].Country);
        Assert.Contains("Argentina", storesFromDb[0].ShippingCountries);

        var eventsFromDb = await db.BoardGameEvents.ToListAsync();
        Assert.Empty(eventsFromDb);

        var userPrefsFromDb = await db.UserPreferences.ToListAsync();
        Assert.Empty(userPrefsFromDb);
    }
}
