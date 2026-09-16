using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Ludeka.Web.Authentication;

/// <summary>
/// Evalúa las políticas de permiso releyendo el <c>AppUser</c> de la sesión en un scope nuevo.
/// Se registra como Singleton para no arrastrar el <c>DbContext</c> del circuito: una lectura
/// rastreada devolvería permisos obsoletos y la suspensión o revocación en caliente no surtiría efecto.
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PermissionAuthorizationHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
    }

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
        var user = await users.GetByIdAsync(userId);

        // HasPermission ya deniega a las cuentas suspendidas y solo concede a la Mesa Fundadora
        // o a un moderador con la bandera exacta.
        if (user is not null && user.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}
