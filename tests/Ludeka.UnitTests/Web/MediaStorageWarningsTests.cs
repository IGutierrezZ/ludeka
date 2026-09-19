using System;
using Ludeka.Application.Options;
using Ludeka.Web;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// INC-48 (PR1a), spec `media-storage-precedence`, requisito 3: en <c>Production</c> sin
/// credenciales válidas de Cloudflare R2, el arranque debe advertir que el almacén de medios activo
/// es degradado, sin abortar. Misma técnica que
/// <see cref="Ludeka.Web.Authentication.ExternalAuthenticationSchemes.GetConfigurationWarnings"/>
/// (<c>WebAuthenticationRegistrationTests</c>): función estática pura, sin arrancar ningún host.
/// </summary>
public class MediaStorageWarningsTests
{
    private static CloudflareR2Options InvalidR2Options() => new()
    {
        Simulate = true // valor por defecto de appsettings.json: nunca son credenciales válidas
    };

    private static CloudflareR2Options ValidR2Options() => new()
    {
        Simulate = false,
        AccountId = "cuenta-test",
        AccessKeyId = "clave-test",
        SecretAccessKey = "secreto-test",
        BucketName = "ludeka-media"
    };

    [Fact]
    public void Production_SinR2Valido_EmiteAvisoDeDegradacion()
    {
        var warnings = MediaStorageWarnings.GetConfigurationWarnings(InvalidR2Options(), "Production");

        Assert.Contains(warnings, warning => warning.Contains("degradado", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Production_ConR2Valido_NoEmiteAviso()
    {
        var warnings = MediaStorageWarnings.GetConfigurationWarnings(ValidR2Options(), "Production");

        Assert.Empty(warnings);
    }

    [Fact]
    public void FueraDeProduction_SinR2Valido_NoEmiteAviso()
    {
        var warnings = MediaStorageWarnings.GetConfigurationWarnings(InvalidR2Options(), "Development");

        Assert.Empty(warnings);
    }
}
