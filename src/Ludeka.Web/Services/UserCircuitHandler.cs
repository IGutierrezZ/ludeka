using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Web.Authentication;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Ludeka.Web.Services;

/// <summary>
/// Resuelve la identidad de la sesión al abrir el circuito (INC-46 F3). El estado de autenticación
/// en cascada aporta el principal con el que arrancó el circuito —la conexión inicial queda como
/// respaldo— y el <c>AppUser</c> se relee sin rastreo desde el repositorio para que la suspensión o
/// la revocación de permisos no queden congeladas en el <c>DbContext</c> del circuito. Un fallo de
/// resolución degrada a sesión sin privilegios: nunca tumba el circuito ni inventa un usuario.
/// </summary>
public sealed class UserCircuitHandler : CircuitHandler
{
    private readonly UserIdentitySnapshot _snapshot;
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly IUserRepository _users;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly ILogger<UserCircuitHandler>? _logger;

    public UserCircuitHandler(
        UserIdentitySnapshot snapshot,
        AuthenticationStateProvider authenticationStateProvider,
        IUserRepository users,
        IHttpContextAccessor? httpContextAccessor = null,
        ILogger<UserCircuitHandler>? logger = null)
    {
        _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        _authenticationStateProvider = authenticationStateProvider ?? throw new ArgumentNullException(nameof(authenticationStateProvider));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    /// <inheritdoc />
    public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
        => ResolveIdentityAsync(cancellationToken);

    /// <summary>Principal sin identidad: sesión degradada, nunca una identidad inventada.</summary>
    private static ClaimsPrincipal AnonymousPrincipal => new(new ClaimsIdentity());

    /// <summary>
    /// Puebla el snapshot del ámbito con la identidad de la sesión. Es idempotente y se puede
    /// invocar fuera del circuito (pruebas o re-resolución explícita).
    /// </summary>
    public async Task ResolveIdentityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var principal = await GetSessionPrincipalAsync();
            var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userId))
            {
                _snapshot.Resolve(principal, user: null);
                return;
            }

            var user = await _users.GetByIdAsync(userId, cancellationToken);
            if (user is null)
            {
                // Cookie válida de una cuenta que ya no existe: no se inventa identidad.
                _logger?.LogWarning(
                    "La sesión apunta al usuario '{UserId}', que no existe en la base de datos; se degrada a sesión sin privilegios.",
                    userId);

                _snapshot.Resolve(AnonymousPrincipal, user: null);
                return;
            }

            _snapshot.Resolve(principal, user);
        }
        catch (Exception exception)
        {
            _logger?.LogError(
                exception,
                "No se pudo resolver la identidad de la sesión al abrir el circuito; se degrada a sesión sin privilegios.");

            _snapshot.Resolve(AnonymousPrincipal, user: null);
        }
    }

    private async Task<ClaimsPrincipal?> GetSessionPrincipalAsync()
    {
        var state = await _authenticationStateProvider.GetAuthenticationStateAsync();
        if (state.User?.Identity?.IsAuthenticated == true)
        {
            return state.User;
        }

        // Respaldo: según el modo de hospedaje, el estado en cascada puede llegar anónimo al
        // circuito aunque la conexión inicial venga autenticada.
        var connectionUser = _httpContextAccessor?.HttpContext?.User;
        if (connectionUser?.Identity?.IsAuthenticated == true)
        {
            return connectionUser;
        }

        return state.User;
    }
}
