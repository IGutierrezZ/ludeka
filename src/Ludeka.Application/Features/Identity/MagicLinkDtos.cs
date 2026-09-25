using Ludeka.Core.Entities;

namespace Ludeka.Application.Features.Identity;

/// <summary>
/// Resultado de la solicitud de un enlace mágico.
/// </summary>
public sealed record MagicLinkRequestResult(
    bool Success,
    string Message,
    string? DevTokenLink = null);

/// <summary>
/// Resultado de la verificación y consumo de un enlace mágico.
/// </summary>
public sealed record MagicLinkVerifyResult(
    bool Success,
    AppUser? User = null,
    string? ErrorMessage = null,
    string? ReturnUrl = null);
