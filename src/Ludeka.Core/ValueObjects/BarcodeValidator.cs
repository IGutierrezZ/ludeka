using System;
using System.Text.RegularExpressions;

namespace Ludeka.Core.ValueObjects;

/// <summary>
/// Validador y normalizador de códigos de barras comerciales estándar (EAN-13 / GTIN-13 / UPC-A).
/// </summary>
public static class BarcodeValidator
{
    private static readonly Regex CleanRegex = new(@"[\s-]+", RegexOptions.Compiled);

    /// <summary>
    /// Comprueba si una cadena representa un código EAN-13 válido con dígito de control correcto.
    /// </summary>
    public static bool IsValidEan13(string? rawBarcode)
    {
        if (string.IsNullOrWhiteSpace(rawBarcode))
            return false;

        string clean = CleanRegex.Replace(rawBarcode.Trim(), string.Empty);
        if (clean.Length != 13)
            return false;

        return ValidateEan13Digits(clean);
    }

    /// <summary>
    /// Intenta limpiar y normalizar una entrada a formato EAN-13 (13 dígitos).
    /// Si la entrada es un UPC-A válido de 12 dígitos, se normaliza añadiendo un '0' a la izquierda (GTIN-13).
    /// </summary>
    public static bool TryNormalizeEan13(string? rawBarcode, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(rawBarcode))
            return false;

        string clean = CleanRegex.Replace(rawBarcode.Trim(), string.Empty);

        // Si es UPC-A de 12 dígitos, convertir a GTIN-13 con 0 inicial
        if (clean.Length == 12)
        {
            clean = "0" + clean;
        }

        if (clean.Length != 13)
            return false;

        if (!ValidateEan13Digits(clean))
            return false;

        normalized = clean;
        return true;
    }

    /// <summary>
    /// Calcula el dígito de control (13º dígito) para una cadena de 12 dígitos numéricos.
    /// Algoritmo módulo 10 con pesos alternos 1 y 3.
    /// </summary>
    public static char CalculateEan13CheckDigit(string first12Digits)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(first12Digits);
        string clean = CleanRegex.Replace(first12Digits.Trim(), string.Empty);

        if (clean.Length != 12)
            throw new ArgumentException("Deben proporcionarse exactamente 12 dígitos numéricos.", nameof(first12Digits));

        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            char c = clean[i];
            if (c < '0' || c > '9')
                throw new ArgumentException($"El carácter '{c}' no es un dígito numérico válido.", nameof(first12Digits));

            int digit = c - '0';
            sum += (i % 2 == 0) ? digit : digit * 3;
        }

        int remainder = sum % 10;
        int checkDigit = (10 - remainder) % 10;
        return (char)('0' + checkDigit);
    }

    private static bool ValidateEan13Digits(string clean)
    {
        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            char c = clean[i];
            if (c < '0' || c > '9')
                return false;

            int digit = c - '0';
            sum += (i % 2 == 0) ? digit : digit * 3;
        }

        char lastChar = clean[12];
        if (lastChar < '0' || lastChar > '9')
            return false;

        int expectedCheckDigit = (10 - (sum % 10)) % 10;
        return (lastChar - '0') == expectedCheckDigit;
    }
}
