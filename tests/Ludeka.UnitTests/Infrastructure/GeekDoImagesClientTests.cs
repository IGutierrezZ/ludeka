using System.Net.Http;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;
using Ludeka.Infrastructure.Bgg;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class GeekDoImagesClientTests
{
    [Fact]
    public void ParseGeekDoImagesJson_SelectsTopVotedImagesPerCategory()
    {
        // Arrange
        string json = """
        {
          "images": [
            {
              "imageid": "1",
              "name": "front_low",
              "caption": "Cover front old",
              "numpositive": 5,
              "canonicaltype": "boxartfront",
              "imageurl": "https://cf.geekdo-images.com/front_low.jpg"
            },
            {
              "imageid": "2",
              "name": "front_high",
              "caption": "Box front 4K",
              "numpositive": 120,
              "canonicaltype": "boxartfront",
              "images": {
                "original": { "src": "https://cf.geekdo-images.com/front_high.jpg" }
              }
            },
            {
              "imageid": "3",
              "name": "back_cover",
              "caption": "Box back",
              "numpositive": 45,
              "canonicaltype": "boxartback",
              "imageurl": "https://cf.geekdo-images.com/back.jpg"
            },
            {
              "imageid": "4",
              "name": "table_setup",
              "caption": "Gameplay in action",
              "numpositive": 95,
              "canonicaltype": "gameplay",
              "images": {
                "large": { "src": "https://cf.geekdo-images.com/table_large.jpg" }
              }
            }
          ]
        }
        """;

        // Act
        var result = GeekDoImagesClient.ParseGeekDoImagesJson(json, 224517);

        // Assert
        Assert.Equal("https://cf.geekdo-images.com/front_high.jpg", result.FrontCoverUrl);
        Assert.Equal("https://cf.geekdo-images.com/back.jpg", result.BackCoverUrl);
        Assert.Equal("https://cf.geekdo-images.com/table_large.jpg", result.TableOrGameplayUrl);
    }

    [Fact]
    public void ParseGeekDoImagesJson_WhenNoBackCoverExists_ReturnsNullForBackCover()
    {
        // Arrange
        string json = """
        {
          "images": [
            {
              "imageid": "1",
              "name": "front",
              "numpositive": 30,
              "canonicaltype": "boxartfront",
              "imageurl": "https://cf.geekdo-images.com/front.jpg"
            }
          ]
        }
        """;

        // Act
        var result = GeekDoImagesClient.ParseGeekDoImagesJson(json, 100);

        // Assert
        Assert.Equal("https://cf.geekdo-images.com/front.jpg", result.FrontCoverUrl);
        Assert.Null(result.BackCoverUrl);
        Assert.Null(result.TableOrGameplayUrl);
    }

    [Fact]
    public async Task GetTopVotedImagesAsync_InSimulateMode_ReturnsSimulatedDeterministicUrls()
    {
        // Arrange
        var options = Options.Create(new BggMassIngestionOptions { Simulate = true });
        using var httpClient = new HttpClient();
        var client = new GeekDoImagesClient(httpClient, options, NullLogger<GeekDoImagesClient>.Instance);

        // Act
        var result = await client.GetTopVotedImagesAsync(342942);

        // Assert
        Assert.NotNull(result.FrontCoverUrl);
        Assert.Contains("342942", result.FrontCoverUrl);
        Assert.NotNull(result.BackCoverUrl);
        Assert.NotNull(result.TableOrGameplayUrl);
    }

    [Fact]
    public void ParseGeekDoImagesJson_WithRealGeekDoSchema_ExtractsImageUrlLg_AndIgnoresMicro()
    {
        // Arrange: Esquema real de la API de GeekDo (imageurl_lg, numrecommend, micro thumbnail)
        string json = """
        {
          "images": [
            {
              "imageid": "3756288",
              "caption": "Nemesis box front cover official",
              "numrecommend": 110,
              "imageurl_lg": "https://cf.geekdo-images.com/front__large/pic3756288.jpg",
              "imageurl": "https://cf.geekdo-images.com/front__micro/fit-in/64x64/pic3756288.jpg"
            },
            {
              "imageid": "4706239",
              "caption": "Retail edition back of the box",
              "numrecommend": 25,
              "imageurl_lg": "https://cf.geekdo-images.com/back__large/pic4706239.jpg",
              "imageurl": "https://cf.geekdo-images.com/back__micro/fit-in/64x64/pic4706239.jpg"
            },
            {
              "imageid": "5482520",
              "caption": "Game in play on table with all components",
              "numrecommend": 42,
              "imageurl_lg": "https://cf.geekdo-images.com/table__large/pic5482520.jpg",
              "imageurl": "https://cf.geekdo-images.com/table__micro/fit-in/64x64/pic5482520.jpg"
            }
          ]
        }
        """;

        // Act
        var result = GeekDoImagesClient.ParseGeekDoImagesJson(json, 167355);

        // Assert: Todas las URLs deben ser __large en alta definición, nunca __micro
        Assert.Equal("https://cf.geekdo-images.com/front__large/pic3756288.jpg", result.FrontCoverUrl);
        Assert.Equal("https://cf.geekdo-images.com/back__large/pic4706239.jpg", result.BackCoverUrl);
        Assert.Equal("https://cf.geekdo-images.com/table__large/pic5482520.jpg", result.TableOrGameplayUrl);
    }

    [Fact]
    public void ParseGeekDoCategoryJson_ExtractsTopVotedHighResImage()
    {
        // Arrange
        string json = """
        {
          "images": [
            {
              "imageid": "10",
              "numrecommend": 3,
              "imageurl_lg": "https://cf.geekdo-images.com/back1__large.jpg"
            },
            {
              "imageid": "20",
              "numrecommend": 45,
              "imageurl_lg": "https://cf.geekdo-images.com/back2__large.jpg"
            }
          ]
        }
        """;

        // Act
        var result = GeekDoImagesClient.ParseGeekDoCategoryJson(json);

        // Assert: debe tomar la de 45 recomendaciones
        Assert.Equal("https://cf.geekdo-images.com/back2__large.jpg", result);
    }

    [Fact]
    public void ParseGeekDoImagesJson_WhenOnlyMicroUrlExists_RejectsAndReturnsNull()
    {
        // Arrange: elemento que solo dispone de miniatura micro de 64px
        string json = """
        {
          "images": [
            {
              "imageid": "99",
              "caption": "Front box",
              "numrecommend": 10,
              "imageurl": "https://cf.geekdo-images.com/uhFK8tUbz7in96NAQoqpxw__micro/img/pic99.jpg"
            }
          ]
        }
        """;

        // Act
        var result = GeekDoImagesClient.ParseGeekDoImagesJson(json, 99);

        // Assert: se rechaza la miniatura micro para evitar imágenes borrosas
        Assert.Null(result.FrontCoverUrl);
    }
}
