using Ludeka.Infrastructure.Bgg;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class BggOptionsTests
{
    [Fact]
    public void ShouldSimulate_WhenSimulateApiIsTrue_ReturnsTrueRegardlessOfToken()
    {
        var optionsWithToken = new BggOptions
        {
            SimulateApi = true,
            ApiToken = "sample-valid-token-12345"
        };

        var optionsWithoutToken = new BggOptions
        {
            SimulateApi = true,
            ApiToken = null
        };

        Assert.True(optionsWithToken.ShouldSimulate);
        Assert.True(optionsWithoutToken.ShouldSimulate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ShouldSimulate_WhenSimulateApiIsFalseButTokenIsEmpty_ReturnsFalse(string? emptyToken)
    {
        var options = new BggOptions
        {
            SimulateApi = false,
            ApiToken = emptyToken
        };

        Assert.False(options.ShouldSimulate);
    }

    [Fact]
    public void ShouldSimulate_WhenSimulateApiIsFalseAndTokenIsPresent_ReturnsFalse()
    {
        var options = new BggOptions
        {
            SimulateApi = false,
            ApiToken = "valid-bearer-token"
        };

        Assert.False(options.ShouldSimulate);
    }

    [Fact]
    public void DefaultConstructor_HasSimulateApiTrueByDefault()
    {
        var options = new BggOptions();

        Assert.True(options.SimulateApi);
        Assert.True(options.ShouldSimulate);
    }

    [Fact]
    public void BearerToken_SyncsBidirectionally_WithApiToken()
    {
        var options = new BggOptions();
        options.BearerToken = "token-123";
        Assert.Equal("token-123", options.ApiToken);

        options.ApiToken = "token-456";
        Assert.Equal("token-456", options.BearerToken);
    }

    [Fact]
    public void ShouldSimulate_WhenSimulateApiIsFalseAndApiKeyIsPresent_ReturnsFalse()
    {
        var options = new BggOptions
        {
            SimulateApi = false,
            ApiKey = "api-key-test-999"
        };

        Assert.False(options.ShouldSimulate);
    }

    [Fact]
    public void DefaultResilienceValues_AreCorrectlyConfigured()
    {
        var options = new BggOptions();

        Assert.Equal(6, options.MaxPollingRetries);
        Assert.Equal(45, options.PollingTimeoutSeconds);
        Assert.Equal(3, options.InitialPollingDelaySeconds);
        Assert.Equal(3, options.MaxRateLimitRetries);
        Assert.Equal("LudekaApp/1.0 (https://ludeka.es; contacto@ludeka.es)", options.UserAgent);
    }
}
