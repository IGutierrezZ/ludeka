using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ludeka.Application.Features.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class BggDumpParserTests
{
    [Fact]
    public async Task ParseRanksDumpAsync_FiltersByMinUsersRated_AndExtractsCorrectFields()
    {
        // Arrange
        string csvContent = """
            id,name,yearpublished,rank,bayesaverage,average,usersrated
            174430,"Gloomhaven",2017,1,8.42,8.61,62000
            224517,"Brass: Birmingham",2018,2,8.41,8.60,48000
            999999,"Prototipo Olvidado",2024,15000,5.1,5.2,14
            167791,"Terraforming Mars",2016,5,8.25,8.39,95000
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var results = await BggDumpParser.ParseRanksDumpAsync(stream, minUsersRated: 30).ToListAsync();

        // Assert
        Assert.Equal(3, results.Count); // Prototipo Olvidado (14 votos) debe ser descartado
        Assert.DoesNotContain(results, r => r.BggId == 999999);

        var gloomhaven = results.First(r => r.BggId == 174430);
        Assert.Equal("Gloomhaven", gloomhaven.Title);
        Assert.Equal(2017, gloomhaven.YearPublished);
        Assert.Equal(1, gloomhaven.BggRank);
        Assert.Equal(62000, gloomhaven.UsersRated);
        Assert.Equal(8.42, gloomhaven.BayesAverage);
        Assert.Equal(8.61, gloomhaven.AverageRating);

        var brass = results.First(r => r.BggId == 224517);
        Assert.Equal("Brass: Birmingham", brass.Title);
        Assert.Equal(48000, brass.UsersRated);
    }

    [Fact]
    public async Task ParseRanksDumpAsync_SupportsAlternativeHeaderNames()
    {
        // Arrange
        string csvContent = """
            bgg_id,title,year,boardgame_rank,bayes_average,rating,num_votes
            342942,"Ark Nova",2021,4,8.35,8.53,42000
            """;

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));

        // Act
        var results = await BggDumpParser.ParseRanksDumpAsync(stream, minUsersRated: 10).ToListAsync();

        // Assert
        Assert.Single(results);
        var item = results[0];
        Assert.Equal(342942, item.BggId);
        Assert.Equal("Ark Nova", item.Title);
        Assert.Equal(2021, item.YearPublished);
        Assert.Equal(4, item.BggRank);
        Assert.Equal(42000, item.UsersRated);
    }

    [Fact]
    public void ParseCsvLine_HandlesQuotesAndCommasCorrectly()
    {
        // Arrange
        string line = "123,\"Juego, Con Comas y \"\"Comillas\"\"\",2020,50";

        // Act
        var fields = BggDumpParser.ParseCsvLine(line);

        // Assert
        Assert.Equal(4, fields.Count);
        Assert.Equal("123", fields[0]);
        Assert.Equal("Juego, Con Comas y \"Comillas\"", fields[1]);
        Assert.Equal("2020", fields[2]);
        Assert.Equal("50", fields[3]);
    }
}
