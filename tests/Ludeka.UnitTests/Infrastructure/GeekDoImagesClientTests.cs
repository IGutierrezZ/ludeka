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
}
