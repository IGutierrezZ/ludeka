using System;
using Ludeka.Infrastructure.Options;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class DatabaseProviderTests
{
    [Fact]
    public void DatabaseOptions_DefaultValues_AreSafeForProduction()
    {
        var options = new DatabaseOptions();

        Assert.Null(options.Provider);
        Assert.False(options.SeedDemoData);
    }

    [Theory]
    [InlineData("PostgreSql")]
    [InlineData("postgresql")]
    [InlineData("Postgres")]
    [InlineData("POSTGRES")]
    [InlineData("Npgsql")]
    public void IsPostgreSql_WhenProviderIsConfiguredAsPostgres_ReturnsTrue(string provider)
    {
        var options = new DatabaseOptions { Provider = provider };

        bool result = options.IsPostgreSql("Data Source=cualquier_cosa.db");

        Assert.True(result);
    }

    [Fact]
    public void IsPostgreSql_WhenProviderIsSqlite_ReturnsFalseEvenWithEmptyConnection()
    {
        var options = new DatabaseOptions { Provider = "Sqlite" };

        bool result = options.IsPostgreSql(null);

        Assert.False(result);
    }

    [Theory]
    [InlineData("Host=db.abcxyz.supabase.co;Port=5432;Database=postgres;Username=postgres;Password=secret")]
    [InlineData("Server=db.abcxyz.supabase.co;Port=5432;Database=postgres;User Id=postgres;Password=secret")]
    [InlineData("postgres://postgres:secret@db.abcxyz.supabase.co:5432/postgres")]
    [InlineData("postgresql://postgres:secret@db.abcxyz.supabase.co:5432/postgres")]
    [InlineData("Port=5432;Database=postgres;")]
    public void IsPostgreSql_AutoDetection_WhenConnectionStringHasPostgresIndicators_ReturnsTrue(string connString)
    {
        var options = new DatabaseOptions(); // Sin provider explícito

        bool result = options.IsPostgreSql(connString);

        Assert.True(result);
    }

    [Theory]
    [InlineData("Data Source=ludeka.db")]
    [InlineData("Data Source=/app/data/ludeka.db")]
    [InlineData("Data Source=tests.db;Mode=Memory;Cache=Shared")]
    [InlineData("")]
    [InlineData(null)]
    public void IsPostgreSql_AutoDetection_WhenConnectionStringIsSqliteOrEmpty_ReturnsFalse(string? connString)
    {
        var options = new DatabaseOptions();

        bool result = options.IsPostgreSql(connString);

        Assert.False(result);
    }

    [Fact]
    public void AdminUserOptions_DefaultValues_AreConsistent()
    {
        var options = new AdminUserOptions();

        Assert.Equal("admin-fundador", options.Id);
        Assert.Equal("Administrador Ludeka", options.UserName);
        Assert.Equal("admin@ludeka.es", options.Email);
        Assert.Equal("España", options.Country);
    }
}
