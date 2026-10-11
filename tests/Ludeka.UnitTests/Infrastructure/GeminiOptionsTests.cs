using Ludeka.Infrastructure.Services;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class GeminiOptionsTests
{
    [Fact]
    public void DefaultOptions_HaveSafeDefaults()
    {
        var options = new GeminiOptions();

        Assert.True(options.Simulate);
        Assert.True(options.ShouldSimulate);
        Assert.Equal("gemini-3.5-flash-lite", options.Model);
        Assert.Equal("gemini-3.5-flash-lite", options.GetEffectiveModel());
        Assert.Equal("https://generativelanguage.googleapis.com/v1beta/", options.BaseUrl);
        Assert.Null(options.ApiKey);
    }

    [Fact]
    public void GetModelCandidateChain_DefaultOptions_ReturnsOrderedCandidatesWithoutDuplicates()
    {
        var options = new GeminiOptions();
        var chain = options.GetModelCandidateChain();

        Assert.NotEmpty(chain);
        Assert.Equal("gemini-3.5-flash-lite", chain[0]);
        Assert.Contains("gemini-3.5-flash", chain);
        Assert.Contains("gemini-3.1-flash-lite", chain);
        Assert.Contains("gemini-3.6-flash", chain);
        Assert.Equal(chain.Distinct(StringComparer.OrdinalIgnoreCase).Count(), chain.Count);
    }

    [Fact]
    public void GetModelCandidateChain_CustomModel_PutsCustomModelFirstWithoutDuplicates()
    {
        var options = new GeminiOptions
        {
            Model = "gemini-3.6-flash"
        };
        var chain = options.GetModelCandidateChain();

        Assert.Equal("gemini-3.6-flash", chain[0]);
        Assert.Equal(chain.Distinct(StringComparer.OrdinalIgnoreCase).Count(), chain.Count);
    }

    [Fact]
    public void GetEffectiveModel_WhenEnvironmentVariableIsSet_TakesPrecedence()
    {
        try
        {
            Environment.SetEnvironmentVariable("Gemini__Model", "gemini-test-env");
            var options = new GeminiOptions
            {
                Model = "gemini-from-config"
            };

            Assert.Equal("gemini-test-env", options.GetEffectiveModel());
        }
        finally
        {
            Environment.SetEnvironmentVariable("Gemini__Model", null);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("auto")]
    [InlineData("AUTO")]
    public void GetEffectiveModel_WhenEmptyOrAuto_AutoSelectsDefaultModel(string? inputModel)
    {
        var options = new GeminiOptions
        {
            Model = inputModel!
        };

        Assert.Equal(GeminiOptions.DefaultModel, options.GetEffectiveModel());
    }

    [Fact]
    public void GetEffectiveModel_WhenCustomModelSpecified_ReturnsCustomModel()
    {
        var options = new GeminiOptions
        {
            Model = "gemini-1.5-pro"
        };

        Assert.Equal("gemini-1.5-pro", options.GetEffectiveModel());
    }

    [Fact]
    public void ShouldSimulate_WhenSimulateIsTrue_ReturnsTrueRegardlessOfApiKey()
    {
        var optionsWithKey = new GeminiOptions
        {
            Simulate = true,
            ApiKey = "AIzaSyFakeKey123456"
        };

        var optionsWithoutKey = new GeminiOptions
        {
            Simulate = true,
            ApiKey = null
        };

        Assert.True(optionsWithKey.ShouldSimulate);
        Assert.True(optionsWithoutKey.ShouldSimulate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ShouldSimulate_WhenSimulateIsFalseButKeyIsEmpty_ReturnsTrue(string? emptyKey)
    {
        var options = new GeminiOptions
        {
            Simulate = false,
            ApiKey = emptyKey
        };

        Assert.True(options.ShouldSimulate);
    }

    [Fact]
    public void ShouldSimulate_WhenSimulateIsFalseAndKeyIsPresent_ReturnsFalse()
    {
        var options = new GeminiOptions
        {
            Simulate = false,
            ApiKey = "AIzaSyValidRealKey987654"
        };

        Assert.False(options.ShouldSimulate);
    }
}
