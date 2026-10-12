using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Ludeka.Application.Contracts;

namespace Ludeka.Application.Features.Seo;

/// <summary>
/// Implementación de generación y emisión del sitemap XML conforme al protocolo oficial de Sitemaps 0.9.
/// </summary>
public class SitemapService : ISitemapService
{
    private readonly IGameRepository _gameRepository;
    private readonly IPublisherRepository? _publisherRepository;
    private readonly ICreatorRepository? _creatorRepository;
    private readonly IStoreRepository? _storeRepository;

    public SitemapService(
        IGameRepository gameRepository,
        IPublisherRepository? publisherRepository = null,
        ICreatorRepository? creatorRepository = null,
        IStoreRepository? storeRepository = null)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _publisherRepository = publisherRepository;
        _creatorRepository = creatorRepository;
        _storeRepository = storeRepository;
    }

    public async Task<IReadOnlyList<SitemapUrlEntry>> GetSitemapEntriesAsync(string baseUrl = "https://ludeka.es", CancellationToken ct = default)
    {
        var normalizedBase = (baseUrl ?? "https://ludeka.es").TrimEnd('/');
        var entries = new List<SitemapUrlEntry>();

        // 1. Rutas canónicas y editoriales clave
        entries.Add(new($"{normalizedBase}/", Priority: 1.0, ChangeFrequency: "daily"));
        entries.Add(new($"{normalizedBase}/catalogo", Priority: 0.9, ChangeFrequency: "daily"));
        entries.Add(new($"{normalizedBase}/novedades", Priority: 0.8, ChangeFrequency: "daily"));
        entries.Add(new($"{normalizedBase}/sorteos", Priority: 0.8, ChangeFrequency: "daily"));
        entries.Add(new($"{normalizedBase}/eventos", Priority: 0.8, ChangeFrequency: "weekly"));
        entries.Add(new($"{normalizedBase}/clasificaciones", Priority: 0.7, ChangeFrequency: "weekly"));
        entries.Add(new($"{normalizedBase}/editoriales", Priority: 0.7, ChangeFrequency: "weekly"));
        entries.Add(new($"{normalizedBase}/creadores", Priority: 0.7, ChangeFrequency: "weekly"));
        entries.Add(new($"{normalizedBase}/tiendas", Priority: 0.7, ChangeFrequency: "weekly"));
        entries.Add(new($"{normalizedBase}/transparencia", Priority: 0.5, ChangeFrequency: "monthly"));

        // 2. Fichas de catálogo dinámicas
        try
        {
            var games = await _gameRepository.GetGameSitemapEntriesAsync(ct).ConfigureAwait(false);
            if (games != null)
            {
                foreach (var g in games)
                {
                    if (string.IsNullOrWhiteSpace(g.Slug)) continue;
                    entries.Add(new(
                        Location: $"{normalizedBase}/juegos/{Uri.EscapeDataString(g.Slug)}",
                        LastModified: g.UpdatedAt,
                        ChangeFrequency: "weekly",
                        Priority: 0.8));
                }
            }
        }
        catch
        {
            // Resiliencia defensiva: ante cualquier fallo de acceso a datos, se devuelven al menos las rutas estáticas
        }

        // 3. Directorio de editoriales
        if (_publisherRepository != null)
        {
            try
            {
                var publishers = await _publisherRepository.GetAllAsync(ct).ConfigureAwait(false);
                if (publishers != null)
                {
                    foreach (var p in publishers)
                    {
                        if (string.IsNullOrWhiteSpace(p.Slug)) continue;
                        entries.Add(new(
                            Location: $"{normalizedBase}/editoriales/{Uri.EscapeDataString(p.Slug)}",
                            LastModified: p.UpdatedAt,
                            ChangeFrequency: "monthly",
                            Priority: 0.6));
                    }
                }
            }
            catch { }
        }

        // 4. Directorio de creadores
        if (_creatorRepository != null)
        {
            try
            {
                var creators = await _creatorRepository.GetAllAsync(ct).ConfigureAwait(false);
                if (creators != null)
                {
                    foreach (var c in creators)
                    {
                        if (string.IsNullOrWhiteSpace(c.Slug)) continue;
                        entries.Add(new(
                            Location: $"{normalizedBase}/creadores/{Uri.EscapeDataString(c.Slug)}",
                            LastModified: c.UpdatedAt,
                            ChangeFrequency: "monthly",
                            Priority: 0.6));
                    }
                }
            }
            catch { }
        }

        // 5. Directorio de tiendas
        if (_storeRepository != null)
        {
            try
            {
                var stores = await _storeRepository.GetAllAsync(ct).ConfigureAwait(false);
                if (stores != null)
                {
                    foreach (var s in stores)
                    {
                        if (string.IsNullOrWhiteSpace(s.Slug)) continue;
                        entries.Add(new(
                            Location: $"{normalizedBase}/tiendas/{Uri.EscapeDataString(s.Slug)}",
                            LastModified: s.UpdatedAt,
                            ChangeFrequency: "monthly",
                            Priority: 0.6));
                    }
                }
            }
            catch { }
        }

        return entries;
    }

    public async Task<string> GenerateSitemapXmlAsync(string baseUrl = "https://ludeka.es", CancellationToken ct = default)
    {
        var entries = await GetSitemapEntriesAsync(baseUrl, ct).ConfigureAwait(false);

        var settings = new XmlWriterSettings
        {
            Async = true,
            Encoding = new UTF8Encoding(false),
            Indent = true,
            OmitXmlDeclaration = false
        };

        using var memoryStream = new MemoryStream();
        await using (var writer = XmlWriter.Create(memoryStream, settings))
        {
            await writer.WriteStartDocumentAsync().ConfigureAwait(false);
            writer.WriteStartElement("urlset", "http://www.sitemaps.org/schemas/sitemap/0.9");

            foreach (var entry in entries)
            {
                writer.WriteStartElement("url");
                writer.WriteElementString("loc", entry.Location);

                if (entry.LastModified.HasValue)
                {
                    writer.WriteElementString("lastmod", entry.LastModified.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }

                if (!string.IsNullOrWhiteSpace(entry.ChangeFrequency))
                {
                    writer.WriteElementString("changefreq", entry.ChangeFrequency);
                }

                if (entry.Priority.HasValue)
                {
                    writer.WriteElementString("priority", entry.Priority.Value.ToString("0.0", CultureInfo.InvariantCulture));
                }

                writer.WriteEndElement(); // </url>
            }

            writer.WriteEndElement(); // </urlset>
            await writer.WriteEndDocumentAsync().ConfigureAwait(false);
            await writer.FlushAsync().ConfigureAwait(false);
        }

        return Encoding.UTF8.GetString(memoryStream.ToArray());
    }
}
