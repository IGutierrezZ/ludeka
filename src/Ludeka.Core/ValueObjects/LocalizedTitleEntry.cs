using System;

namespace Ludeka.Core.ValueObjects;

/// <summary>
/// Representa el título comercial con el que se vende un juego en un país o territorio determinado.
/// </summary>
public record LocalizedTitleEntry(
    string CountryCode,
    string Title
)
{
    public LocalizedTitleEntry() : this(string.Empty, string.Empty) { }
}
