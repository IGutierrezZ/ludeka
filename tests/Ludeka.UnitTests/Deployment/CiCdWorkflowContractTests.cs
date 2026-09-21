using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace Ludeka.UnitTests.Deployment;

/// <summary>
/// Contrato de INC-52 (Fase 6, D11/D12): el paso "Desplegar revisión en Google Cloud Run" de
/// <c>ci-cd.yml</c> debe declarar al menos un proveedor de autenticación externo utilizable y
/// el correo del Administrador Fundador, sin filtrar ningún secreto en texto plano por
/// <c>env_vars</c>. El paso "Publicar la revisión de los Cloud Run Jobs" no atiende HTTP, no
/// puede recibir un retorno OAuth y no siembra al fundador, así que no debe imitar este
/// cableado por simetría mal entendida (pin de regresión hacia delante). Ayudantes de lectura
/// de fuente con copia propia, no compartida con <c>AuthorizationPipelineContractTests</c>
/// (convención del repositorio: cada contrato de fuente lleva su propia copia).
/// </summary>
public class CiCdWorkflowContractTests
{
    private const string EnvVarsMarker = "env_vars: |";
    private const string SecretsMarker = "secrets: |";
    private const string NextStepMarker = "      - name:";
    private const string CloudRunJobsStepMarker = "- name: Publicar la revisión de los Cloud Run Jobs";

    private static readonly Regex EnabledProviderPattern = new(
        @"Authentication__Providers__(?<provider>[A-Za-z]+)__Enabled=true",
        RegexOptions.Compiled);

    [Fact]
    public void WebEnvVarsBlock_ShouldEnableAtLeastOneAuthenticationProvider()
    {
        // Y1 (spec production-auth-bootstrap:53-57): el paso web debe declarar al menos un
        // proveedor de autenticación externo como habilitado.
        var envVarsBlock = GetWebEnvVarsBlock();
        var enabledProviders = ExtractEnabledProviders(envVarsBlock);

        Assert.True(
            enabledProviders.Count > 0,
            "El bloque env_vars del paso web no declara ningún Authentication__Providers__{P}__Enabled=true.");
    }

    [Fact]
    public void WebSecretsBlock_ShouldResolveFullCredentialsForEveryEnabledProvider()
    {
        // Y2 (spec production-auth-bootstrap:53-57): para cada proveedor habilitado según Y1,
        // su identificador de cliente y su secreto deben resolverse desde secrets, ambos con
        // sufijo :latest.
        var envVarsBlock = GetWebEnvVarsBlock();
        var enabledProviders = ExtractEnabledProviders(envVarsBlock);

        Assert.True(
            enabledProviders.Count > 0,
            "No hay ningún proveedor habilitado en env_vars; Y2 no tiene ningún {P} que comprobar.");

        var secretsBlock = GetWebSecretsBlock();

        foreach (var provider in enabledProviders)
        {
            var clientIdValue = GetSecretValue(secretsBlock, $"Authentication__Providers__{provider}__ClientId")
                ?? GetSecretValue(secretsBlock, $"Authentication__Providers__{provider}__AppId");
            var clientSecretValue = GetSecretValue(secretsBlock, $"Authentication__Providers__{provider}__ClientSecret")
                ?? GetSecretValue(secretsBlock, $"Authentication__Providers__{provider}__AppSecret");

            Assert.False(
                string.IsNullOrEmpty(clientIdValue),
                $"El proveedor {provider} está habilitado pero no tiene ClientId/AppId en el bloque secrets.");
            Assert.False(
                string.IsNullOrEmpty(clientSecretValue),
                $"El proveedor {provider} está habilitado pero no tiene ClientSecret/AppSecret en el bloque secrets.");

            Assert.EndsWith(":latest", clientIdValue!, StringComparison.Ordinal);
            Assert.EndsWith(":latest", clientSecretValue!, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void WebSecretsBlock_ShouldSupplyTheFoundingAdminEmail()
    {
        // Y3 (spec production-auth-bootstrap:41-45): el correo del fundador debe llegar desde
        // el almacén de secretos del proveedor de despliegue.
        var secretsBlock = GetWebSecretsBlock();

        Assert.True(
            Regex.IsMatch(secretsBlock, @"^\s*AdminUser__Email=", RegexOptions.Multiline),
            "El bloque secrets del paso web no contiene una línea AdminUser__Email=.");
    }

    [Fact]
    public void WebEnvVarsBlock_ShouldNeverLeakAnySecretInPlainText()
    {
        // Y4 (spec production-auth-bootstrap:59-63): ningún secreto de proveedor ni el correo
        // del fundador puede aparecer en texto plano en env_vars.
        var envVarsBlock = GetWebEnvVarsBlock();

        Assert.DoesNotContain("ClientSecret", envVarsBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("AppSecret", envVarsBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminUser__Email", envVarsBlock, StringComparison.Ordinal);
    }

    [Fact]
    public void CloudRunJobsStep_ShouldNeverWireAuthenticationOrFoundingAdminKeys()
    {
        // Y5 (design.md D11, pin de regresión hacia delante): los Cloud Run Jobs no atienden
        // HTTP, no reciben el retorno OAuth y no siembran al fundador; no deben cablear estas
        // claves aunque parezca simétrico añadirlas junto a las del servicio web.
        var jobsBlock = GetCloudRunJobsBlock();

        Assert.DoesNotContain("Authentication__", jobsBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminUser__", jobsBlock, StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ExtractEnabledProviders(string envVarsBlock)
    {
        return EnabledProviderPattern.Matches(envVarsBlock)
            .Select(match => match.Groups["provider"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

    private static string? GetSecretValue(string block, string key)
    {
        var match = Regex.Match(block, $@"^\s*{Regex.Escape(key)}=(?<value>.+)$", RegexOptions.Multiline);
        return match.Success ? match.Groups["value"].Value.TrimEnd() : null;
    }

    private static string GetWebEnvVarsBlock()
    {
        var source = ReadSource(".github/workflows/ci-cd.yml");
        var envVarsIndex = source.IndexOf(EnvVarsMarker, StringComparison.Ordinal);
        Assert.True(envVarsIndex >= 0, "ci-cd.yml no contiene ningún bloque env_vars: |.");

        var secretsIndex = source.IndexOf(SecretsMarker, envVarsIndex, StringComparison.Ordinal);
        Assert.True(secretsIndex > envVarsIndex, "ci-cd.yml no contiene un bloque secrets: | después de env_vars: |.");

        return source[envVarsIndex..secretsIndex];
    }

    private static string GetWebSecretsBlock()
    {
        var source = ReadSource(".github/workflows/ci-cd.yml");
        var secretsIndex = source.IndexOf(SecretsMarker, StringComparison.Ordinal);
        Assert.True(secretsIndex >= 0, "ci-cd.yml no contiene ningún bloque secrets: |.");

        var nextStepIndex = source.IndexOf(NextStepMarker, secretsIndex + SecretsMarker.Length, StringComparison.Ordinal);
        Assert.True(nextStepIndex > secretsIndex, "No se encontró el siguiente paso tras el bloque secrets: | del despliegue web.");

        return source[secretsIndex..nextStepIndex];
    }

    private static string GetCloudRunJobsBlock()
    {
        var source = ReadSource(".github/workflows/ci-cd.yml");
        var jobsStepIndex = source.IndexOf(CloudRunJobsStepMarker, StringComparison.Ordinal);
        Assert.True(jobsStepIndex >= 0, "ci-cd.yml no contiene el paso 'Publicar la revisión de los Cloud Run Jobs'.");

        return source[jobsStepIndex..];
    }

    private static string ReadSource(string relativePath)
    {
        var path = Path.Combine(GetRepoRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(path), $"No se encontró el archivo fuente: {relativePath}");
        return File.ReadAllText(path);
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
