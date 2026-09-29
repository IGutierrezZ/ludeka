using System;
using System.IO;
using Ludeka.Application.Options;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Contrato de marcado y configuración para canales comunitarios y enlaces de apoyo (INC-56).
/// Verifica que MainLayout y Transparency consuman de forma accesible y segura las opciones
/// tipadas de CommunityNotificationOptions y que las reglas de afiliados incluyan Amazon.
/// </summary>
public class CommunityAndSupportLinksContractTests
{
    private const string MainLayoutPath = "src/Ludeka.Web/Components/Layout/MainLayout.razor";
    private const string TransparencyPath = "src/Ludeka.Web/Components/Pages/Transparency.razor";

    [Fact]
    public void MainLayout_ShouldRenderKofiSupportLinkWithSecurityAndAccessibilityAttributes()
    {
        var source = ReadSource(MainLayoutPath);

        // Inyección de opciones comunitarias
        Assert.Contains("CommunityNotificationOptions", source, StringComparison.Ordinal);

        // Bloque condicional y enlace a Ko-fi
        Assert.Contains("CommunityOptions?.Value?.KofiUrl", source, StringComparison.Ordinal);
        Assert.Contains("href=\"@CommunityOptions.Value.KofiUrl\"", source, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", source, StringComparison.Ordinal);
        Assert.Contains("rel=\"noopener noreferrer\"", source, StringComparison.Ordinal);
        Assert.Contains("title=\"Apoyar el proyecto en Ko-fi\"", source, StringComparison.Ordinal);
        Assert.Contains("Apoyar en Ko-fi", source, StringComparison.Ordinal);
        Assert.Contains("Name=\"coffee\"", source, StringComparison.Ordinal);

        // Foco visible para accesibilidad WCAG 2.2 AA
        Assert.Contains("focus-visible:ring-2", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MainLayout_ShouldPreserveDiscordAndTelegramCommunityChannels()
    {
        var source = ReadSource(MainLayoutPath);

        Assert.Contains("CommunityOptions?.Value?.DiscordInviteUrl", source, StringComparison.Ordinal);
        Assert.Contains("href=\"@CommunityOptions.Value.DiscordInviteUrl\"", source, StringComparison.Ordinal);
        Assert.Contains("Comunidad Discord", source, StringComparison.Ordinal);

        Assert.Contains("CommunityOptions?.Value?.TelegramChannelUrl", source, StringComparison.Ordinal);
        Assert.Contains("href=\"@CommunityOptions.Value.TelegramChannelUrl\"", source, StringComparison.Ordinal);
        Assert.Contains("Canal Telegram", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparencyPage_ShouldInjectCommunityOptionsAndRenderDynamicSupportLinks()
    {
        var source = ReadSource(TransparencyPath);

        // Debe inyectar IOptions<CommunityNotificationOptions>
        Assert.Contains("@inject Microsoft.Extensions.Options.IOptions<Ludeka.Application.Options.CommunityNotificationOptions> CommunityOptions", source, StringComparison.Ordinal);

        // Debe usar las URLs tipadas configuradas en lugar de enlaces fijos genéricos
        Assert.Contains("@CommunityOptions.Value.KofiUrl", source, StringComparison.Ordinal);
        Assert.Contains("@CommunityOptions.Value.DiscordInviteUrl", source, StringComparison.Ordinal);
        Assert.Contains("@CommunityOptions.Value.TelegramChannelUrl", source, StringComparison.Ordinal);

        // No debe contener URLs genéricas hardcodeadas para Ko-fi o Discord sin pasar por las opciones
        Assert.DoesNotContain("href=\"https://ko-fi.com\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"https://discord.com\"", source, StringComparison.Ordinal);

        // Atributos de seguridad y accesibilidad
        Assert.Contains("rel=\"noopener noreferrer\"", source, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", source, StringComparison.Ordinal);
        Assert.Contains("focus-visible:ring-2", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CommunityNotificationOptions_ShouldHaveDefaultKofiUrlConfigured()
    {
        var options = new CommunityNotificationOptions();

        Assert.Equal("https://ko-fi.com/ludeka", options.KofiUrl);
    }

    [Fact]
    public void CommunityNotificationOptions_ShouldHaveDefaultDiscordInviteUrlConfigured()
    {
        var options = new CommunityNotificationOptions();

        Assert.Equal("https://discord.gg/DgGUUEU6gs", options.DiscordInviteUrl);
    }

    [Fact]
    public void AffiliateOptions_ShouldIncludeAmazonInDefaultRules()
    {
        var rules = AffiliateOptions.CreateDefaultRules();

        Assert.True(rules.ContainsKey("Amazon"), "Las reglas por defecto deben contener la entrada para 'Amazon'.");
        var amazonRule = rules["Amazon"];
        Assert.Equal("tag", amazonRule.ParamName);
        Assert.Equal("ludeka-21", amazonRule.AffiliateTag);
        Assert.Equal("amazon.es", amazonRule.DomainMatch);
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
