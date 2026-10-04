using System;
using Ludeka.Core.ValueObjects;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class BarcodeValidatorTests
{
    [Theory]
    [InlineData("8436017220100")] // Catan (Devir)
    [InlineData("4006381333931")] // Standard EAN-13
    [InlineData("8436017220124")] // Carcassonne (Devir)
    [InlineData("9780201379624")] // Book ISBN-13
    public void IsValidEan13_ShouldReturnTrue_ForValidEan13(string ean)
    {
        Assert.True(BarcodeValidator.IsValidEan13(ean));
    }

    [Theory]
    [InlineData("8436017220101")] // Bad check digit (expected 0, got 1)
    [InlineData("4006381333930")] // Bad check digit
    [InlineData("1234567890125")] // Bad check digit (expected 8, got 5)
    [InlineData("84360172201")]   // Too short (11 digits)
    [InlineData("84360172201001")] // Too long (14 digits)
    [InlineData("843601722010a")] // Non-numeric
    [InlineData("")]              // Empty
    [InlineData(null)]            // Null
    public void IsValidEan13_ShouldReturnFalse_ForInvalidEan13(string? ean)
    {
        Assert.False(BarcodeValidator.IsValidEan13(ean));
    }

    [Fact]
    [Trait("Category", "Domain")]
    public void TryNormalizeEan13_ShouldHandleHyphensAndSpaces()
    {
        // 8436-0172-2010-0 with hyphens and spaces
        string raw = " 8436-0172 2010-0 ";
        bool ok = BarcodeValidator.TryNormalizeEan13(raw, out string normalized);

        Assert.True(ok);
        Assert.Equal("8436017220100", normalized);
    }

    [Theory]
    [InlineData("012345678905", "0012345678905")] // UPC-A 12 digits converted to GTIN-13
    public void TryNormalizeEan13_ShouldConvertValidUpcToGtin13(string upc, string expectedEan13)
    {
        bool ok = BarcodeValidator.TryNormalizeEan13(upc, out string normalized);

        Assert.True(ok);
        Assert.Equal(expectedEan13, normalized);
    }

    [Theory]
    [InlineData("843601722010", '0')]
    [InlineData("400638133393", '1')]
    [InlineData("978020137962", '4')]
    public void CalculateEan13CheckDigit_ShouldComputeCorrectChecksum(string first12Digits, char expectedCheckDigit)
    {
        char calculated = BarcodeValidator.CalculateEan13CheckDigit(first12Digits);
        Assert.Equal(expectedCheckDigit, calculated);
    }
}
