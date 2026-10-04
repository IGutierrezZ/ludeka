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
        if (string.IsNullOrWhiteSpace(first12Digits) || first12Digits.Length != 12)
            throw new ArgumentException("Se requieren exactamente 12 dígitos para calcular el dígito de control EAN-13.", nameof(first12Digits));

        int sum = 0;
        for (int i = 0; i < 12; i++)
        {
            char c = first12Digits[i];
            if (c < '0' || c > '9')
                throw new ArgumentException("Todos los caracteres deben ser dígitos numéricos.", nameof(first12Digits));

            int digit = c - '0';
            // Índices pares (0, 2, 4...) peso 1; impares (1, 3, 5...) peso 3
            sum += (i % 2 == 0) ? digit * 1 : digit * 3;
        }

        int remainder = sum % 10;
        int checkDigit = (remainder == 0) ? 0 : 10 - remainder;

        return (char)('0' + checkDigit);
    }

    private static bool ValidateEan13Digits(string clean13)
    {
        for (int i = 0; i < 13; i++)
        {
            if (clean13[i] < '0' || clean13[i] > '9')
                return false;
        }

        char expected = CalculateEan13CheckDigit(clean13.Substring(0, 12));
        return clean13[12] == expected;
    }
}
