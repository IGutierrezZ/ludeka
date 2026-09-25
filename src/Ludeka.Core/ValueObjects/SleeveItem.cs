using System;

namespace Ludeka.Core.ValueObjects;

public record SleeveItem(
    string FormatName,
    double WidthMm,
    double HeightMm,
    int CardCount,
    string? AffiliateUrl,
    string? StoreName = null,
    string? Country = null,
    IReadOnlyList<string>? ShippingCountries = null)
{
    private readonly string _formatName = ValidateFormatName(FormatName);
    private readonly double _widthMm = ValidateDimension(WidthMm, nameof(WidthMm));
    private readonly double _heightMm = ValidateDimension(HeightMm, nameof(HeightMm));
    private readonly int _cardCount = ValidateCardCount(CardCount);

    public string FormatName
    {
        get => _formatName;
        init => _formatName = ValidateFormatName(value);
    }

    public double WidthMm
    {
        get => _widthMm;
        init => _widthMm = ValidateDimension(value, nameof(WidthMm));
    }

    public double HeightMm
    {
        get => _heightMm;
        init => _heightMm = ValidateDimension(value, nameof(HeightMm));
    }

    public int CardCount
    {
        get => _cardCount;
        init => _cardCount = ValidateCardCount(value);
    }

    private static string ValidateFormatName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre del formato no puede ser nulo ni vacío.", nameof(FormatName));
        return name;
    }

    private static double ValidateDimension(double dimension, string paramName)
    {
        if (dimension is < 30.0 or > 250.0)
            throw new ArgumentOutOfRangeException(paramName, dimension, $"La dimensión {paramName} debe estar entre 30.0 y 250.0 mm.");
        return dimension;
    }

    private static int ValidateCardCount(int count)
    {
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(CardCount), count, "La cantidad de cartas no puede ser negativa.");
        return count;
    }

    public int CalculatePacksNeeded(int packSize = 50)
    {
        if (packSize <= 0) throw new ArgumentOutOfRangeException(nameof(packSize), "El tamaño de paquete debe ser mayor a 0.");
        if (CardCount <= 0) return 0;
        return (int)Math.Ceiling((double)CardCount / packSize);
    }

    public int PacksNeeded50 => CalculatePacksNeeded(50);
    public int PacksNeeded100 => CalculatePacksNeeded(100);

    public string DimensionText => $"{WidthMm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} x {HeightMm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} mm";

    public bool ShipsTo(string? targetCountry)
    {
        if (string.IsNullOrWhiteSpace(targetCountry))
            return true;

        if (string.IsNullOrWhiteSpace(Country))
            return true;

        var normalizedTarget = CountryCatalog.Normalize(targetCountry);

        if (string.Equals(CountryCatalog.Normalize(Country), normalizedTarget, StringComparison.OrdinalIgnoreCase))
            return true;

        if (CountryCatalog.IsInternational(Country))
            return true;

        if (ShippingCountries != null && ShippingCountries.Any(c => string.Equals(CountryCatalog.Normalize(c), normalizedTarget, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }
}
