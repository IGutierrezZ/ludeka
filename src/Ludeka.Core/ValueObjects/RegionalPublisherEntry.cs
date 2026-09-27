using System;

namespace Ludeka.Core.ValueObjects;

/// <summary>
/// Representa una editorial local o licenciataria de un juego en un país o territorio determinado.
/// </summary>
public record RegionalPublisherEntry(
    string CountryCode,
    string CountryName,
    string PublisherName,
    string? PublisherSlug = null
)
{
    public RegionalPublisherEntry() : this(string.Empty, string.Empty, string.Empty, null) { }
}
