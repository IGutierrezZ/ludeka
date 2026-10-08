using System;
using System.Collections.Generic;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Estructura ligera para la primera fase de filtrado y ordenación en memoria del catálogo.
/// Contiene únicamente las facetas esenciales, evitando la descarga y serialización de campos
/// pesados como descripciones, resúmenes IA o galerías multimedia completas.
/// </summary>
public sealed record GameFilterIndexItem(
    Guid Id,
    IReadOnlyList<ScalabilityEntry> Scalability,
    GameDuration Duration,
    AgeRating Age,
    GameStyle Style,
    int? BggRank,
    double BggRating,
    int YearPublished = 0,
    string SpanishTitle = "",
    double? BggWeight = null
);
