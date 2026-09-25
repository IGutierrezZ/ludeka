using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Microsoft.Extensions.Options;

namespace Ludeka.Application.Features.Identity;

/// <inheritdoc />
public sealed class AccountConnectionsService : IAccountConnectionsService
{
    private readonly IExternalLoginRepository _externalLogins;
    private readonly ICurrentUserService _currentUser;
    private readonly AuthenticationOptions _options;

    // Caché de ámbito (INC-49, diseño §D6): el resultado se calcula una única vez por instancia,
    // que en este servicio Scoped equivale a una vez por petición SSR o por circuito.
    private Task<AccountConnectionsView>? _cachedView;

    /// <inheritdoc />
    public event EventHandler? Invalidated;

    public AccountConnectionsService(
        IExternalLoginRepository externalLogins,
        ICurrentUserService currentUser,
        IOptions<AuthenticationOptions> options)
    {
        _externalLogins = externalLogins ?? throw new ArgumentNullException(nameof(externalLogins));
        _currentUser = currentUser ?? throw new ArgumentNullException(nameof(currentUser));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
    }

    /// <inheritdoc />
    public Task<AccountConnectionsView> GetConnectionsAsync(CancellationToken cancellationToken = default)
        => _cachedView ??= BuildViewAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> HasVerifiedProviderEmailAsync(CancellationToken cancellationToken = default)
    {
        var view = await GetConnectionsAsync(cancellationToken);
        return view.HasVerifiedProviderEmail;
    }

    /// <inheritdoc />
    public void InvalidateCache()
    {
        _cachedView = null;
        Invalidated?.Invoke(this, EventArgs.Empty);
    }

    private async Task<AccountConnectionsView> BuildViewAsync(CancellationToken cancellationToken)
    {
        var userId = _currentUser.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            // Sin sesión no hay cuenta que avisar (INC-49, diseño §D6): vista vacía, nunca una
            // excepción de anonimia. Esta lectura no es una operación de escritura.
            return new AccountConnectionsView([], HasVerifiedProviderEmail: false, CanUnlink: false);
        }

        var links = await _externalLogins.ListByUserIdAsync(userId, cancellationToken);

        var connections = ExternalProviderNames.All
            .Where(name => _options.Providers.TryGetValue(name, out var provider) && provider.IsUsable)
            .Select(name =>
            {
                var link = links.FirstOrDefault(l => string.Equals(l.Provider, name, StringComparison.OrdinalIgnoreCase));
                return new AccountConnectionDto(
                    name,
                    link is not null,
                    link?.LinkedAt,
                    link?.ProviderEmailVerifiedAt is not null,
                    link?.ProviderEmail,
                    link?.ProviderEmailVerifiedAt);
            })
            .ToList();

        return new AccountConnectionsView(
            connections,
            HasVerifiedProviderEmail: links.Any(l => l.ProviderEmailVerifiedAt is not null),
            CanUnlink: links.Count > 1);
    }
}
