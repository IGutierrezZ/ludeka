using System;
using System.Collections.Generic;
using Ludeka.Core.Entities;

namespace Ludeka.Application.Features.Identity;

/// <summary>Resultado de un intento de vinculación desde sesión activa (INC-49).</summary>
public enum ExternalLoginLinkOutcome
{
    /// <summary>Se creó la fila y el proveedor queda vinculado a la cuenta de la sesión.</summary>
    Linked,

    /// <summary>Ese mismo par ya pertenecía a esta cuenta: no se crea nada y no es un error.</summary>
    AlreadyLinkedToThisAccount,

    /// <summary>El par pertenece a otra cuenta: se rechaza sin mover ni duplicar la fila.</summary>
    RejectedOwnedByAnotherAccount
}

/// <summary>Resultado tipado de <see cref="IExternalLoginService.LinkAsync"/>.</summary>
public sealed record ExternalLoginLinkResult(
    ExternalLoginLinkOutcome Outcome,
    string Provider,
    AppUser User,
    bool AccountEmailReplaced = false);

/// <summary>
/// Estado de vinculación de un proveedor concreto para la cuenta de la sesión. Sin consumidor hasta
/// PR #3/#4 (contrato de lectura <c>IAccountConnectionsService</c> y pantalla de conexiones).
/// </summary>
public sealed record AccountConnectionDto(
    string Provider, bool IsLinked, DateTimeOffset? LinkedAt, bool ProviderEmailVerified);

/// <summary>
/// Vista completa de las conexiones de la cuenta de la sesión. Sin consumidor hasta PR #3/#4.
/// </summary>
public sealed record AccountConnectionsView(
    IReadOnlyList<AccountConnectionDto> Connections,
    bool HasVerifiedProviderEmail,
    bool CanUnlink);
