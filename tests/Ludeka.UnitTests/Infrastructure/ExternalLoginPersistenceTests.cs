using System;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class ExternalLoginPersistenceTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private LudekaDbContext _context = null!;
    private DbContextOptions<LudekaDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        await _connection.OpenAsync();

        _options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new LudekaDbContext(_options);
        await _context.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private async Task<AppUser> SeedUserAsync(string id)
    {
        var user = new AppUser(id, $"Usuario {id}", $"{id}@ludeka.es");
        _context.AppUsers.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    [Fact]
    public void Model_ShouldDeclareUniqueIndexOnProviderAndProviderKey()
    {
        // Act
        var entity = _context.Model.FindEntityType(typeof(ExternalLogin));
        var index = entity?.GetIndexes()
            .SingleOrDefault(i => i.Properties.Select(p => p.Name).SequenceEqual(["Provider", "ProviderKey"]));

        // Assert
        Assert.NotNull(index);
        Assert.True(index!.IsUnique);
        Assert.Equal("ExternalLogins", entity!.GetTableName());
    }

    [Fact]
    public void Model_ShouldDeclareCascadeForeignKeyToAppUsers()
    {
        // Act
        var entity = _context.Model.FindEntityType(typeof(ExternalLogin));
        var foreignKey = entity?.GetForeignKeys()
            .SingleOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(AppUser));

        // Assert
        Assert.NotNull(foreignKey);
        Assert.Equal(nameof(ExternalLogin.UserId), foreignKey!.Properties.Single().Name);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.True(foreignKey.IsRequired);
    }

    [Fact]
    public async Task Repository_ShouldPersistAndRetrieveLogin_ByProviderAndProviderKey()
    {
        // Arrange
        await SeedUserAsync("user-abc");
        IExternalLoginRepository repository = new ExternalLoginRepository(_context);
        var login = new ExternalLogin("user-abc", "Google", "google-sub-123", "jugador@ludeka.es");

        // Act
        await repository.AddAsync(login);
        var retrieved = await repository.GetByProviderKeyAsync("Google", "google-sub-123");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(login.Id, retrieved!.Id);
        Assert.Equal("user-abc", retrieved.UserId);
        Assert.Equal("Google", retrieved.Provider);
        Assert.Equal("google-sub-123", retrieved.ProviderKey);
        Assert.Equal("jugador@ludeka.es", retrieved.ProviderEmail);
        Assert.Equal(login.LinkedAt, retrieved.LinkedAt);
    }

    [Fact]
    public async Task Repository_ShouldReturnNull_WhenThePairIsUnknown()
    {
        // Arrange
        await SeedUserAsync("user-xyz");
        IExternalLoginRepository repository = new ExternalLoginRepository(_context);
        await repository.AddAsync(new ExternalLogin("user-xyz", "Discord", "discord-1"));

        // Act
        var retrieved = await repository.GetByProviderKeyAsync("Discord", "discord-2");
        var unknownProvider = await repository.GetByProviderKeyAsync("Facebook", "discord-1");

        // Assert
        Assert.Null(retrieved);
        Assert.Null(unknownProvider);
    }

    [Fact]
    public async Task UniqueIndex_ShouldRejectRepeatedProviderPair()
    {
        // Arrange
        await SeedUserAsync("user-a");
        await SeedUserAsync("user-b");
        IExternalLoginRepository repository = new ExternalLoginRepository(_context);
        await repository.AddAsync(new ExternalLogin("user-a", "Google", "clave-compartida"));

        // Act & Assert: el par (Provider, ProviderKey) no puede repetirse aunque cambie el usuario
        await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.AddAsync(new ExternalLogin("user-b", "Google", "clave-compartida")));
    }

    [Fact]
    public async Task DeleteCascade_ShouldRemoveLogins_WhenTheUserIsDeleted()
    {
        // Arrange
        await SeedUserAsync("user-cascade");
        IExternalLoginRepository repository = new ExternalLoginRepository(_context);
        await repository.AddAsync(new ExternalLogin("user-cascade", "Facebook", "fb-42"));

        // Act: borrado directo del principal para probar el ON DELETE CASCADE del esquema
        using (var deleteCmd = _connection.CreateCommand())
        {
            deleteCmd.CommandText = "DELETE FROM \"AppUsers\" WHERE \"Id\" = 'user-cascade';";
            await deleteCmd.ExecuteNonQueryAsync();
        }

        // Assert
        using var freshContext = new LudekaDbContext(_options);
        var orphan = await freshContext.ExternalLogins
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.ProviderKey == "fb-42");
        Assert.Null(orphan);
    }

    [Fact]
    public async Task SqliteSchemaMigrator_ShouldCreateExternalLoginsTable_OnLegacyDatabase()
    {
        // Arrange: base de datos antigua (con Games, sin ExternalLogins)
        using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

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
                """;
            await createCmd.ExecuteNonQueryAsync();
        }

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(connection)
            .Options;
        using var db = new LudekaDbContext(options);

        // Act
        await SqliteSchemaMigrator.EnsureSchemaUpToDateAsync(db);

        // Assert: la tabla nueva existe y es consultable con el modelo completo
        Assert.Empty(await db.ExternalLogins.AsNoTracking().ToListAsync());

        // Assert: el índice único y el índice de la FK quedaron creados en SQLite
        using var indexCmd = connection.CreateCommand();
        indexCmd.CommandText = """
            SELECT COUNT(*) FROM sqlite_master
            WHERE type = 'index' AND tbl_name = 'ExternalLogins'
              AND name IN ('IX_ExternalLogins_Provider_ProviderKey', 'IX_ExternalLogins_UserId');
            """;
        var indexCount = Convert.ToInt32(await indexCmd.ExecuteScalarAsync());
        Assert.Equal(2, indexCount);
    }
}
