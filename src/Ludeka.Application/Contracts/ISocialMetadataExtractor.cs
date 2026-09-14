using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.DTOs;

namespace Ludeka.Application.Contracts;

/// <summary>
/// Extractor ligero de metadatos OpenGraph, oEmbed y miniaturas de YouTube/Instagram sin APIs de pago.
/// </summary>
public interface ISocialMetadataExtractor
{
    Task<SocialMetadataResultDto?> ExtractFromUrlAsync(string url, CancellationToken ct = default);
}
