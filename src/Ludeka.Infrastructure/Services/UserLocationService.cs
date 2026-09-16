using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Ludeka.Application.Contracts;
using Ludeka.Core.Entities;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Infrastructure.Services;

/// <summary>
/// Implementación scoped de IUserLocationService para gestión de ubicación activa y ordenación prioritaria territorial.
/// </summary>
public class UserLocationService : IUserLocationService
{
    private readonly IUserPreferenceService? _userPreferenceService;
    private readonly ICurrentUserService? _currentUserService;

    private string? _userCountry;
    private string? _detectedCountry;

    public UserLocationService(
        IUserPreferenceService? userPreferenceService = null,
        ICurrentUserService? currentUserService = null)
    {
        _userPreferenceService = userPreferenceService;
        _currentUserService = currentUserService;
    }

    public string? CurrentCountry => _userCountry;
    public string? DetectedCountry => _detectedCountry;
    public string? EffectiveCountry => _userCountry ?? _detectedCountry;

    public void SetUserCountry(string? country)
    {
        _userCountry = string.IsNullOrWhiteSpace(country)
            ? null
            : CountryCatalog.Normalize(country);
    }

    public void SetDetectedCountry(string? country)
    {
        _detectedCountry = string.IsNullOrWhiteSpace(country)
            ? null
            : CountryCatalog.Normalize(country);
    }

    public async Task<string?> GetEffectiveCountryAsync(CancellationToken cancellationToken = default)
    {
        // Sin sesión no hay identidad que escribir ni preferencia que leer: este servicio solo
        // consulta, de modo que la navegación pública se mantiene y ninguna guarda de anonimia aplica.
        // 1. Si el usuario actual tiene ID, consultar preferencia persistida
        if (_currentUserService != null && !string.IsNullOrWhiteSpace(_currentUserService.UserId) && _userPreferenceService != null)
        {
            try
            {
                var country = await _userPreferenceService.GetUserCountryAsync(_currentUserService.UserId, cancellationToken);
                if (!string.IsNullOrWhiteSpace(country))
                {
                    _userCountry = CountryCatalog.Normalize(country);
                    return _userCountry;
                }
            }
            catch
            {
                // Fallback silencioso a estado en memoria
            }
        }

        // 2. Si no, retornar el país seleccionado en la sesión o detectado
        return EffectiveCountry;
    }

    public Task<string?> GetDetectedCountryAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(DetectedCountry);
    }

    public async Task<UserLocationState> GetUserLocationAsync(CancellationToken cancellationToken = default)
    {
        var effective = await GetEffectiveCountryAsync(cancellationToken);
        return new UserLocationState(CurrentCountry, DetectedCountry, effective);
    }

    public IEnumerable<T> PrioritizeByCountry<T>(IEnumerable<T> items, Func<T, string?> countrySelector, string? preferredCountry = null)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(countrySelector);

        var targetCountry = !string.IsNullOrWhiteSpace(preferredCountry)
            ? CountryCatalog.Normalize(preferredCountry)
            : EffectiveCountry;

        if (string.IsNullOrWhiteSpace(targetCountry))
            return items;

        return items
            .OrderByDescending(item =>
            {
                var country = countrySelector(item);
                if (string.IsNullOrWhiteSpace(country))
                    return 0;

                var normalized = CountryCatalog.Normalize(country);
                if (string.Equals(normalized, targetCountry, StringComparison.OrdinalIgnoreCase))
                    return 2; // Máxima prioridad: país local exacto

                if (CountryCatalog.IsInternational(country))
                    return 1; // Prioridad secundaria: internacional / global

                return 0; // Resto de países
            });
    }

    public IEnumerable<T> PrioritizeByCountry<T>(IEnumerable<T> items, string? preferredCountry = null, Func<T, string?>? countrySelector = null)
    {
        if (countrySelector != null)
        {
            return PrioritizeByCountry(items, countrySelector, preferredCountry);
        }

        return PrioritizeByCountry(items, item => ExtractCountryFromItem(item), preferredCountry);
    }

    private static string? ExtractCountryFromItem(object? item)
    {
        if (item is null)
            return null;

        return item switch
        {
            GamePurchaseLink link => link.Country,
            SleeveItem sleeve => sleeve.Country,
            Store store => store.Country,
            Giveaway giveaway => giveaway.Country,
            BoardGameEvent boardGameEvent => boardGameEvent.Country,
            _ => ExtractCountryViaReflection(item)
        };
    }

    private static string? ExtractCountryViaReflection(object item)
    {
        var prop = item.GetType().GetProperty("Country", BindingFlags.Public | BindingFlags.Instance);
        return prop?.GetValue(item) as string;
    }
}
