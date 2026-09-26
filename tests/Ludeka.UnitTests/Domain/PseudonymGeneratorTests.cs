using System;
using Ludeka.Core.Helpers;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class PseudonymGeneratorTests
{
    [Fact]
    public void Generate_WhenNotAnonymousAndNoCustom_ReturnsOriginalUserId()
    {
        var result = PseudonymGenerator.Generate("user-123", null, isAnonymous: false);
        Assert.Equal("user-123", result);
    }

    [Fact]
    public void Generate_WhenNotAnonymousWithValidCustom_ReturnsCustomPseudonym()
    {
        var result = PseudonymGenerator.Generate("user-123", "ElMeepleErrante", isAnonymous: false);
        Assert.Equal("ElMeepleErrante", result);
    }

    [Fact]
    public void Generate_WhenAnonymousWithNoCustom_GeneratesDeterministicMaskedPseudonym()
    {
        var result1 = PseudonymGenerator.Generate("user-456", null, isAnonymous: true);
        var result2 = PseudonymGenerator.Generate("user-456", null, isAnonymous: true);
        var resultOther = PseudonymGenerator.Generate("user-789", null, isAnonymous: true);

        // Formato determinista no reversible
        Assert.StartsWith("Mesa #", result1);
        Assert.Equal(result1, result2);
        Assert.NotEqual(result1, resultOther);
    }

    [Fact]
    public void Generate_WhenAnonymous_NeverDerivesOrContainsEmailOrUserId()
    {
        string emailLikeId = "alicia.garcia@gmail.com";
        var result = PseudonymGenerator.Generate(emailLikeId, null, isAnonymous: true);

        Assert.DoesNotContain("@", result);
        Assert.DoesNotContain("alicia", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("garcia", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gmail", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Generate_WhenAnonymousWithValidCustom_ReturnsCustomPseudonym()
    {
        var result = PseudonymGenerator.Generate("user-123", "EstrategaMisterioso", isAnonymous: true);
        Assert.Equal("EstrategaMisterioso", result);
    }

    [Theory]
    [InlineData("alberto@correo.com")]
    [InlineData("usuario@ludeka.es")]
    [InlineData("https://miweb.es")]
    [InlineData("http://spam.org")]
    [InlineData("visita www.tienda.com")]
    [InlineData("esto-es-un-seudonimo-excesivamente-largo-para-la-clasificacion-de-jugadores-en-la-comunidad")]
    public void NormalizeCustomPseudonym_ShouldRejectPiiOrLinks(string invalidPseudonym)
    {
        Assert.Throws<ArgumentException>(() => PseudonymGenerator.NormalizeCustomPseudonym(invalidPseudonym));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("TableroMaldito", "TableroMaldito")]
    [InlineData("  Jugador 42  ", "Jugador 42")]
    public void NormalizeCustomPseudonym_ShouldCleanAndReturnExpected(string? input, string? expected)
    {
        var result = PseudonymGenerator.NormalizeCustomPseudonym(input);
        Assert.Equal(expected, result);
    }
}
