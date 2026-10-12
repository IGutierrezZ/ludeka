using System;

namespace Ludeka.Application.DTOs;

/// <summary>
/// Proyección ligera de juego para el mapa del sitio XML (sitemap).
/// </summary>
public record GameSitemapDto(string Slug, DateTimeOffset? UpdatedAt);
