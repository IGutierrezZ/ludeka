using Ludeka.Core.Helpers;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class TextNormalizerTests
{
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("Código 5", "Codigo 5")]
    [InlineData("Agrícola", "Agricola")]
    [InlineData("Borgoña", "Borgona")]
    [InlineData("Pelícanos", "Pelicanos")]
    [InlineData("ÁÉÍÓÚáéíóúñü", "AEIOUaeiounu")]
    [InlineData("Wingspan", "Wingspan")]
    public void RemoveDiacritics_ShouldStripAllDiacriticsAndAccents(string? input, string expected)
    {
        // Act
        var result = TextNormalizer.RemoveDiacritics(input!);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("Código 5", "%codigo%5%")]
    [InlineData("codigo 5", "%codigo%5%")]
    [InlineData("codigo", "%codigo%")]
    [InlineData("código", "%codigo%")]
    [InlineData("Los Castillos de Borgoña", "%los%castillos%de%borgona%")]
    [InlineData("borgona", "%borgona%")]
    [InlineData("borgoña", "%borgona%")]
    [InlineData("Catán", "%catan%")]
    [InlineData("catan", "%catan%")]
    [InlineData("  Wingspan  ", "%wingspan%")]
    [InlineData("7-wonders: duel", "%7%wonders%duel%")]
    public void ToSearchSlugPattern_ShouldGenerateCleanWildcardSlugPattern(string? input, string expected)
    {
        // Act
        var result = TextNormalizer.ToSearchSlugPattern(input!);

        // Assert
        Assert.Equal(expected, result);
    }
}
