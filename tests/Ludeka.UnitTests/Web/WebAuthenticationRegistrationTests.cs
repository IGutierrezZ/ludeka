using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Ludeka.Application.Features.Identity;
using Ludeka.Web.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;
using AuthenticationOptions = Ludeka.Application.Features.Identity.AuthenticationOptions;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Contrato del registro dirigido por configuración de INC-46 (F2): cookie de sesión propia y
/// esquemas sociales que solo existen si el proveedor está habilitado y tiene credenciales.
/// </summary>
public class WebAuthenticationRegistrationTests
{
    private static ExternalProviderOptions Provider(bool enabled, string? clientId = null, string? clientSecret = null)
        => new() { Enabled = enabled, ClientId = clientId, ClientSecret = clientSecret };

    private static ServiceProvider BuildProvider(AuthenticationOptions options)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLudekaAuthentication(options);
        return services.BuildServiceProvider();
    }

    private static AuthenticationOptions OptionsWithUsableGoogle()
    {
        var options = new AuthenticationOptions();
        options.Providers[ExternalProviderNames.Google] = Provider(true, "google-client-id", "google-client-secret");
        return options;
    }

    [Fact]
    public async Task DisabledProvider_ShouldNotRegisterItsScheme()
    {
        // Arrange: los tres proveedores deshabilitados, como en la configuración por defecto
        var options = new AuthenticationOptions();
        options.Providers[ExternalProviderNames.Google] = Provider(false, "google-id", "google-secret");
        options.Providers[ExternalProviderNames.Discord] = Provider(false, "discord-id", "discord-secret");
        options.Providers[ExternalProviderNames.Facebook] = Provider(false, "facebook-id", "facebook-secret");

        using var provider = BuildProvider(options);
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        // Assert
        Assert.Null(await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme));
        Assert.Null(await schemes.GetSchemeAsync("Discord"));
        Assert.Null(await schemes.GetSchemeAsync("Facebook"));
        Assert.NotNull(await schemes.GetSchemeAsync(ExternalAuthenticationSchemes.SessionCookieScheme));
    }

    [Fact]
    public async Task EnabledProviderWithoutCredentials_ShouldNotRegisterItsSchemeAndShouldWarn()
    {
        // Arrange: habilitado pero sin credenciales (hoy no hay secretos en el repositorio)
        var options = new AuthenticationOptions();
        options.Providers[ExternalProviderNames.Discord] = Provider(true);

        using var provider = BuildProvider(options);
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        // Assert
        Assert.Null(await schemes.GetSchemeAsync("Discord"));

        var warnings = ExternalAuthenticationSchemes.GetConfigurationWarnings(options);
        Assert.Contains(warnings, warning => warning.Contains("Discord", StringComparison.Ordinal));
    }

    [Fact]
    public async Task EnabledProviderWithCredentials_ShouldRegisterSchemeWithCallbackSignInSchemeAndScopes()
    {
        // Arrange
        using var provider = BuildProvider(OptionsWithUsableGoogle());
        var schemes = provider.GetRequiredService<IAuthenticationSchemeProvider>();

        // Act
        var scheme = await schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme);

        // Assert
        Assert.NotNull(scheme);
        var google = provider.GetRequiredService<IOptionsMonitor<GoogleOptions>>()
            .Get(GoogleDefaults.AuthenticationScheme);
        Assert.Equal(ExternalAuthenticationSchemes.GoogleCallbackPath, google.CallbackPath);
        Assert.Equal(ExternalAuthenticationSchemes.SessionCookieScheme, google.SignInScheme);
        Assert.Equal("google-client-id", google.ClientId);
        Assert.Equal("google-client-secret", google.ClientSecret);
        Assert.Contains("email", google.Scope);
    }

    [Fact]
    public void SessionCookie_ShouldBeHttpOnlySecureLaxSlidingWithConfiguredExpiry()
    {
        // Arrange
        var options = OptionsWithUsableGoogle();
        options.Cookie.ExpireMinutes = 90;

        using var provider = BuildProvider(options);
        var cookie = provider.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(ExternalAuthenticationSchemes.SessionCookieScheme);

        // Assert
        Assert.True(cookie.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, cookie.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, cookie.Cookie.SameSite);
        Assert.True(cookie.SlidingExpiration);
        Assert.Equal(TimeSpan.FromMinutes(90), cookie.ExpireTimeSpan);
        Assert.Equal(ExternalAuthenticationSchemes.LoginPath, cookie.LoginPath.Value);
        Assert.Equal(ExternalAuthenticationSchemes.LogoutPath, cookie.LogoutPath.Value);
    }

    [Fact]
    public void DefaultConfigurationFile_ShouldDeclareProvidersDisabledAndWithoutUsableCredentials()
    {
        // Arrange: la configuración versionada no contiene credenciales OAuth
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(GetRepoRoot(), "src", "Ludeka.Web", "appsettings.json"), optional: false)
            .Build();

        // Act
        var options = configuration.GetSection(AuthenticationOptions.SectionName).Get<AuthenticationOptions>();

        // Assert
        Assert.NotNull(options);
        var providers = options!.Providers;
        Assert.All(ExternalProviderNames.All, name =>
        {
            Assert.True(providers.ContainsKey(name), $"Falta Authentication:Providers:{name} en appsettings.json");
            Assert.False(providers[name].IsUsable, $"El proveedor {name} no debe quedar utilizable sin credenciales en el repositorio");
        });
        Assert.False(providers[ExternalProviderNames.Facebook].Enabled);
        Assert.True(options.Cookie.EffectiveExpireMinutes > 0);
    }

    private static string GetRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "Ludeka.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
