using System;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;
using Ludeka.Infrastructure.Data;
using Ludeka.Infrastructure.Seeding;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

/// <summary>
/// Contrato del sembrado del directorio (INC-31): purga de diseñadores retirados
/// y re-siembra aditiva desde el padrón estático de creadores de contenido.
/// </summary>
public class DirectorySeederTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LudekaDbContext _dbContext;

    public DirectorySeederTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<LudekaDbContext>()
            .UseSqlite(_connection)
            .Options;

        _dbContext = new LudekaDbContext(options);
        _dbContext.Database.EnsureCreated();
    }

    private static readonly string[] RetiredDesignerSlugs =
    [
        "elizabeth-hargrave", "klaus-teuber", "uwe-rosenberg",
        "bruno-cathala", "jacob-fryxelius", "jamey-stegmaier"
    ];

    /// <summary>Reproduce a mano la DB legada: 6 diseñadores sembrados por el seed antiguo + Sergio.</summary>
    private async Task SeedLegacyDesignersAsync()
    {
        var legacy = new[]
        {
            new Creator("Elizabeth Hargrave", "elizabeth-hargrave", "Estados Unidos",
                "Diseñadora de juegos de mesa y ornitóloga aficionada.", "/images/creators/elizabeth-hargrave.png", 104523),
            new Creator("Klaus Teuber", "klaus-teuber", "Alemania",
                "Leyenda del diseño de juegos de mesa moderno.", "/images/creators/klaus-teuber.png", 84),
            new Creator("Uwe Rosenberg", "uwe-rosenberg", "Alemania",
                "Uno de los autores europeos más prolíficos.", "/images/creators/uwe-rosenberg.png", 10),
            new Creator("Bruno Cathala", "bruno-cathala", "Francia",
                "Maestro del juego de mesa dinámico.", "/images/creators/bruno-cathala.png", 1727),
            new Creator("Jacob Fryxelius", "jacob-fryxelius", "Suecia",
                "Autor sueco creador de Terraforming Mars.", null, 47970),
            new Creator("Jamey Stegmaier", "jamey-stegmaier", "Estados Unidos",
                "Diseñador y cofundador de Stonemaier Games.", "/images/creators/jamey-stegmaier.png", 61168)
        };
        await _dbContext.Creators.AddRangeAsync(legacy);
        await _dbContext.Creators.AddAsync(new Creator(
            "Sergio (Análisis Parálisis)",
            "analisis-paralisis",
            "España",
            "Referente absoluto de la divulgación audiovisual de juegos de mesa en español.",
            "/images/creators/analisis-paralisis.png",
            null,
            "https://analisisparalisis.es",
            new[]
            {
                new SocialNetworkLink(SocialPlatform.YouTube, "https://youtube.com/@AnalisisParalisis", "@AnalisisParalisis", "Canal Análisis Parálisis")
            }));
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task SeedCreatorsAsync_OnLegacyDbWithDesigners_PurgesAndSeedsOnlyContentCreators()
    {
        await SeedLegacyDesignersAsync();

        var result = await DirectorySeeder.SeedDirectoryAsync(_dbContext);

        var creators = await _dbContext.Creators.ToListAsync();
        var publishers = await _dbContext.Publishers.ToListAsync();
        var stores = await _dbContext.Stores.ToListAsync();

        Assert.Equal(35, creators.Count);
        Assert.Equal(46, publishers.Count);
        Assert.Equal(37, stores.Count);

        Assert.Equal(34, result.CreatorsAdded); // 35 en total, 1 ya existía (analisis-paralisis)
        Assert.Equal(46, result.PublishersAdded);
        Assert.Equal(37, result.StoresAdded);

        Assert.DoesNotContain(creators, c => RetiredDesignerSlugs.Contains(c.Slug));

        // "Análisis Parálisis" sobrevive sin duplicarse y conserva su link de YouTube
        var analisisParalisis = creators.Single(c => c.Slug == "analisis-paralisis");
        Assert.Contains(analisisParalisis.SocialLinks, l => l.Platform == SocialPlatform.YouTube);
    }

    [Fact]
    public async Task SeedCreatorsAsync_IsIdempotent_NoDuplicatesOnSecondRun()
    {
        await SeedLegacyDesignersAsync();

        var firstResult = await DirectorySeeder.SeedDirectoryAsync(_dbContext);
        var creatorsAfterFirst = await _dbContext.Creators.CountAsync();
        var publishersAfterFirst = await _dbContext.Publishers.CountAsync();
        var storesAfterFirst = await _dbContext.Stores.CountAsync();

        var secondResult = await DirectorySeeder.SeedDirectoryAsync(_dbContext);
        var creatorsAfterSecond = await _dbContext.Creators.CountAsync();
        var publishersAfterSecond = await _dbContext.Publishers.CountAsync();
        var storesAfterSecond = await _dbContext.Stores.CountAsync();

        Assert.Equal(creatorsAfterFirst, creatorsAfterSecond);
        Assert.Equal(publishersAfterFirst, publishersAfterSecond);
        Assert.Equal(storesAfterFirst, storesAfterSecond);

        Assert.Equal(35, creatorsAfterSecond);
        Assert.Equal(46, publishersAfterSecond);
        Assert.Equal(37, storesAfterSecond);

        Assert.Equal(0, secondResult.CreatorsAdded);
        Assert.Equal(0, secondResult.PublishersAdded);
        Assert.Equal(0, secondResult.StoresAdded);

        var creators = await _dbContext.Creators.ToListAsync();
        Assert.DoesNotContain(creators, c => RetiredDesignerSlugs.Contains(c.Slug));
    }

    [Fact]
    public async Task SeedCreatorsAsync_PreservesCreatorsOutsidePadron()
    {
        // Creador creado manualmente por un moderador en runtime: la purga no debe tocarlo
        await _dbContext.Creators.AddAsync(new Creator(
            "Canal Manual", "mi-canal-manual", "España",
            "Creador de contenido dado de alta por un moderador."));
        await _dbContext.SaveChangesAsync();

        await SeedLegacyDesignersAsync();
        await DirectorySeeder.SeedDirectoryAsync(_dbContext);

        var all = await _dbContext.Creators.ToListAsync();
        Assert.Contains(all, c => c.Slug == "mi-canal-manual");
        Assert.Equal(36, all.Count); // 35 del padrón (incl. analisis-paralisis) + 1 manual
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }
}
