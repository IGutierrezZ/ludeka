using System;
using System.Security.Cryptography;
using System.Text;

namespace Ludeka.Core.Helpers;

/// <summary>
/// Generador y validador de seudónimos seguros y deterministas para clasificaciones públicas,
/// garantizando anonimato estricto y ausencia de datos identificativos (anti-PII).
/// </summary>
public static class PseudonymGenerator
{
    private const int MaxPseudonymLength = 30;

    /// <summary>
    /// Genera o resuelve el seudónimo visible para el jugador.
    /// Si es anónimo y no tiene seudónimo personalizado válido, genera una máscara determinista no reversible
    /// basada en un hash seguro del identificador de usuario ("Mesa #XXXX").
    /// </summary>
    public static string Generate(string userId, string? customPseudonym = null, bool isAnonymous = false)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(userId));
        }

        string cleanUserId = userId.Trim();
        string? normalizedCustom = NormalizeCustomPseudonym(customPseudonym);

        if (!isAnonymous)
        {
            return normalizedCustom ?? cleanUserId;
        }

        if (!string.IsNullOrWhiteSpace(normalizedCustom))
        {
            return normalizedCustom;
        }

        // Generar un sufijo hexadecimal de 4 caracteres no reversible a partir de SHA-256
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(cleanUserId));
        string hex = Convert.ToHexString(hash)[..4].ToUpperInvariant();

        return $"Mesa #{hex}";
    }

    /// <summary>
    /// Valida si un seudónimo personalizado cumple con las políticas de privacidad y seguridad de Ludeka.
    /// Rechaza correos (@), enlaces HTTP/HTTPS, dominios y longitudes excesivas.
    /// </summary>
    public static bool IsValidCustomPseudonym(string? pseudonym, out string? error)
    {
        if (string.IsNullOrWhiteSpace(pseudonym))
        {
            error = null;
            return true;
        }

        string trimmed = pseudonym.Trim();

        if (trimmed.Length > MaxPseudonymLength)
        {
            error = $"El seudónimo no puede superar los {MaxPseudonymLength} caracteres.";
            return false;
        }

        if (trimmed.Contains('@'))
        {
            error = "El seudónimo no puede contener direcciones de correo electrónico (@).";
            return false;
        }

        string lower = trimmed.ToLowerInvariant();
        if (lower.Contains("http://") || lower.Contains("https://") || lower.Contains("www.") ||
            lower.Contains(".com") || lower.Contains(".es") || lower.Contains(".net") || lower.Contains(".org"))
        {
            error = "El seudónimo no puede contener enlaces ni nombres de dominio.";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Normaliza y valida un seudónimo personalizado. Lanza ArgumentException si no cumple las reglas.
    /// Devuelve null si es vacío.
    /// </summary>
    public static string? NormalizeCustomPseudonym(string? pseudonym)
    {
        if (string.IsNullOrWhiteSpace(pseudonym))
        {
            return null;
        }

        string trimmed = pseudonym.Trim();
        if (!IsValidCustomPseudonym(trimmed, out string? error))
        {
            throw new ArgumentException(error, nameof(pseudonym));
        }

        return trimmed;
    }
}
