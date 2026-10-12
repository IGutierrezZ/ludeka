using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Ludeka.Application.Features.Seo;

/// <summary>
/// Entrada individual del mapa del sitio XML conforme al protocolo Sitemaps 0.9.
/// </summary>
public record SitemapUrlEntry(
    string Location,
    DateTimeOffset? LastModified = null,
    string? ChangeFrequency = null,
    double? Priority = null);

/// <summary>
/// Servicio para la generación y consulta de URLs del sitemap XML de Ludeka.
/// </summary>
public interface ISitemapService
{
    Task<IReadOnlyList<SitemapUrlEntry>> GetSitemapEntriesAsync(string baseUrl = "https://ludeka.es", CancellationToken ct = default);
    Task<string> GenerateSitemapXmlAsync(string baseUrl = "https://ludeka.es", CancellationToken ct = default);
}
