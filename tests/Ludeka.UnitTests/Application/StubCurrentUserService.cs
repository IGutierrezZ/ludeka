using System.Collections.Generic;
using Ludeka.Application.Contracts;
using Ludeka.Core.Enums;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// Doble de sustitución de la identidad de la sesión para las pruebas de la política de anonimia
/// (INC-46, F4). <see cref="Anonymous"/> es la sesión inexistente real del contrato;
/// <see cref="PrivilegedWithoutSession"/> es una identidad incoherente que declara privilegios sin
/// sesión y sirve para exigir que ningún servicio escriba confiando solo en las banderas de permiso.
/// </summary>
public sealed class StubCurrentUserService : ICurrentUserService
{
    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public IReadOnlyList<string> Roles { get; set; } = [];

    public bool IsFoundingTeam { get; set; }

    public ModeratorPermission Permissions { get; set; } = ModeratorPermission.None;

    public bool IsInRole(string role) => Roles.Contains(role);

    public bool HasPermission(ModeratorPermission permission)
        => IsFoundingTeam || (IsInRole("Moderator") && (Permissions & permission) == permission);

    /// <summary>Identidad sin sesión: sin usuario, sin roles y sin permisos.</summary>
    public static StubCurrentUserService Anonymous() => new();

    /// <summary>Identidad con sesión iniciada y el identificador indicado.</summary>
    public static StubCurrentUserService WithSession(
        string userId = "sesion-real-1",
        string userName = "Usuario con Sesión") => new()
        {
            UserId = userId,
            UserName = userName
        };

    /// <summary>Identidad con privilegios declarados pero sin sesión.</summary>
    public static StubCurrentUserService PrivilegedWithoutSession() => new()
    {
        IsFoundingTeam = true
    };
}
