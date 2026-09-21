using System;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Data;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class DataProtectionPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<LudekaDbContext> _options;

    public DataProtectionPersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        using var initContext = new LudekaDbContext(_options);
        initContext.Database.EnsureCreated();
    }

    [Fact]
    public void LudekaDbContext_ShouldImplementIDataProtectionKeyContext()
    {
        using var context = new LudekaDbContext(_options);
        Assert.IsAssignableFrom<IDataProtectionKeyContext>(context);
        Assert.NotNull(context.DataProtectionKeys);
    }

    [Fact]
    public void DataProtection_WhenConfiguredWithDbContext_ShouldPersistKeysAndProtectData()
    {
        // Arrange: Servicio con persistencia en DbContext
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<LudekaDbContext>(_ => new LudekaDbContext(_options));
        services.AddDataProtection()
            .SetApplicationName("Ludeka")
            .PersistKeysToDbContext<LudekaDbContext>();

        using var provider = services.BuildServiceProvider();

        // Act: Proteger datos con un protector derivado
        var dataProtectionProvider = provider.GetRequiredService<IDataProtectionProvider>();
        var protector = dataProtectionProvider.CreateProtector("SessionCookieTest");

        const string plaintext = "cookie-session-user-12345";
        var protectedData = protector.Protect(plaintext);
        var roundtrip = protector.Unprotect(protectedData);

        // Assert: El cifrado/descifrado es correcto
        Assert.Equal(plaintext, roundtrip);
        Assert.NotEqual(plaintext, protectedData);

        // Assert: La clave criptográfica se ha persistido en la tabla DataProtectionKeys de LudekaDbContext
        using var verifyContext = new LudekaDbContext(_options);
        var keys = verifyContext.DataProtectionKeys.ToList();
        Assert.NotEmpty(keys);
        Assert.Contains(keys, k => !string.IsNullOrWhiteSpace(k.Xml));
    }

    [Fact]
    public async Task SqliteSchemaMigrator_ShouldEnsureDataProtectionKeysTableExists()
    {
        using var context = new LudekaDbContext(_options);
        await SqliteSchemaMigrator.EnsureSchemaUpToDateAsync(context);

        // Validar que la tabla DataProtectionKeys existe y es consultable
        var count = await context.DataProtectionKeys.CountAsync();
        Assert.True(count >= 0);
    }

    public void Dispose()
    {
        _connection.Dispose();
    }
}
