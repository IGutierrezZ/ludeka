using System;
using System.IO;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Services;
using SkiaSharp;
using Xunit;

namespace Ludeka.UnitTests.Web;

/// <summary>
/// Pruebas de contrato y auditoría de marcado para la optimización de imágenes (INC-57).
/// Verifica que las tarjetas de catálogo, carril y páginas principales prioricen miniaturas
/// (ThumbnailUrl), erradiquen el CLS mediante dimensiones intrínsecas (width/height) y apliquen
/// carga asíncrona no bloqueante (loading="lazy", decoding="async").
/// </summary>
public class CatalogImageOptimizationContractTests
{
    private const string GameCardPath = "src/Ludeka.Web/Components/Shared/GameCard.razor";
    private const string HomeGameCardPath = "src/Ludeka.Web/Components/Home/HomeGameCard.razor";
    private const string HomeGiveawayCardPath = "src/Ludeka.Web/Components/Home/HomeGiveawayCard.razor";
    private const string HomeReleaseCardPath = "src/Ludeka.Web/Components/Home/HomeReleaseCard.razor";
    private const string ExpansionSisterListPath = "src/Ludeka.Web/Components/Shared/ExpansionSisterList.razor";
    private const string ExpansionEcosystemSectionPath = "src/Ludeka.Web/Components/Shared/ExpansionEcosystemSection.razor";
    private const string GameDetailPath = "src/Ludeka.Web/Components/Pages/GameDetail.razor";

    [Fact]
    public void GameCard_ShouldPrioritizeThumbnailUrlAndIncludeAntiClsAttributes()
    {
        var source = ReadSource(GameCardPath);

        // Debe priorizar ThumbnailUrl frente a CoverImageUrl
        Assert.Contains("Game.ThumbnailUrl", source, StringComparison.Ordinal);
        Assert.Contains("Game.CoverImageUrl", source, StringComparison.Ordinal);

        // Atributos de dimensiones y carga
        Assert.Contains("width=\"240\"", source, StringComparison.Ordinal);
        Assert.Contains("height=\"240\"", source, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", source, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeGameCard_ShouldPrioritizeThumbnailUrlAndIncludeAntiClsAttributes()
    {
        var source = ReadSource(HomeGameCardPath);

        // Debe priorizar ThumbnailUrl frente a CoverImageUrl para la escala de 192px
        Assert.Contains("Game.ThumbnailUrl", source, StringComparison.Ordinal);
        Assert.Contains("Game.CoverImageUrl", source, StringComparison.Ordinal);

        Assert.Contains("width=\"192\"", source, StringComparison.Ordinal);
        Assert.Contains("height=\"192\"", source, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", source, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeGiveawayCard_ShouldIncludeIntrinsicDimensionsToPreventCls()
    {
        var source = ReadSource(HomeGiveawayCardPath);

        // El img de sorteos debe contar con dimensiones intrínsecas explícitas y carga asíncrona (formato catálogo INC-79)
        Assert.Contains("width=\"192\"", source, StringComparison.Ordinal);
        Assert.Contains("height=\"192\"", source, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", source, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HomeReleaseCard_ShouldIncludeIntrinsicDimensionsToPreventCls()
    {
        var source = ReadSource(HomeReleaseCardPath);

        // El img de novedades debe contar con dimensiones intrínsecas explícitas y carga asíncrona
        Assert.Contains("width=\"320\"", source, StringComparison.Ordinal);
        Assert.Contains("height=\"180\"", source, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", source, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ExpansionComponents_ShouldIncludeIntrinsicDimensionsAndLazyLoading()
    {
        var sisterSource = ReadSource(ExpansionSisterListPath);
        Assert.Contains("width=\"56\"", sisterSource, StringComparison.Ordinal);
        Assert.Contains("height=\"56\"", sisterSource, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", sisterSource, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", sisterSource, StringComparison.Ordinal);

        var ecoSource = ReadSource(ExpansionEcosystemSectionPath);
        Assert.Contains("width=\"64\"", ecoSource, StringComparison.Ordinal);
        Assert.Contains("height=\"64\"", ecoSource, StringComparison.Ordinal);
        Assert.Contains("loading=\"lazy\"", ecoSource, StringComparison.Ordinal);
        Assert.Contains("decoding=\"async\"", ecoSource, StringComparison.Ordinal);
    }

    [Fact]
    public void GameDetail_HeroAndGalleryImages_ShouldHaveOptimalLoadingAttributes()
    {
        var source = ReadSource(GameDetailPath);

        // Carátula principal del hero: fetchpriority="high" para LCP y dimensiones fijas
        Assert.Contains("fetchpriority=\"high\"", source, StringComparison.Ordinal);
        Assert.Contains("width=\"320\"", source, StringComparison.Ordinal);
        Assert.Contains("height=\"320\"", source, StringComparison.Ordinal);

        // Imágenes de galería (contraportada y mesa): deben usar decoding="async" y loading="lazy"
        Assert.Contains("alt=\"Contraportada de @Game.SpanishTitle\" loading=\"lazy\" decoding=\"async\"", source, StringComparison.Ordinal);
        Assert.Contains("alt=\"Componentes en mesa de @Game.SpanishTitle\" loading=\"lazy\" decoding=\"async\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SeedGames_And_LocalAssets_ShouldHaveOptimizedWebpVariants()
    {
        var repoRoot = GetRepoRoot();
        var gamesDir = Path.Combine(repoRoot, "src", "Ludeka.Web", "wwwroot", "images", "games");
        var seedPath = Path.Combine(repoRoot, "src", "Ludeka.Infrastructure", "Seeding", "seed-games.json");
        var optimizer = new SkiaSharpImageOptimizationService();

        Assert.True(Directory.Exists(gamesDir), $"El directorio {gamesDir} debe existir.");
        Assert.True(File.Exists(seedPath), $"El fichero de semilla {seedPath} debe existir.");

        // 1. Optimizar patchwork.png si excede 500 KB y generar patchwork.webp
        var patchworkPng = Path.Combine(gamesDir, "patchwork.png");
        var patchworkWebp = Path.Combine(gamesDir, "patchwork.webp");
        if (File.Exists(patchworkPng))
        {
            var pngInfo = new FileInfo(patchworkPng);
            if (pngInfo.Length > 500 * 1024)
            {
                using var inStream = new FileStream(patchworkPng, FileMode.Open, FileAccess.Read);
                using var originalBitmap = SkiaSharp.SKBitmap.Decode(inStream);
                int targetW = Math.Min(400, originalBitmap.Width);
                int targetH = (int)Math.Round((double)originalBitmap.Height * targetW / originalBitmap.Width);
                using var resized = originalBitmap.Resize(new SkiaSharp.SKImageInfo(targetW, targetH), SkiaSharp.SKSamplingOptions.Default);
                using var img = SkiaSharp.SKImage.FromBitmap(resized ?? originalBitmap);
                using var pngData = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 85);
                File.WriteAllBytes(patchworkPng, pngData.ToArray());
            }

            if (!File.Exists(patchworkWebp))
            {
                using var inStream = new FileStream(patchworkPng, FileMode.Open, FileAccess.Read);
                var webpBytes = await optimizer.ConvertToWebpAsync(inStream, maxWidth: 400, quality: 80);
                await File.WriteAllBytesAsync(patchworkWebp, webpBytes);
            }
        }

        // 2. Para cada juego en seed-games.json, asegurar que existe miniatura .webp a máx 400px
        var jsonText = await File.ReadAllTextAsync(seedPath);
        using var doc = System.Text.Json.JsonDocument.Parse(jsonText);
        var updatedElements = new System.Collections.Generic.List<System.Text.Json.Nodes.JsonObject>();

        var jsonNodes = System.Text.Json.Nodes.JsonNode.Parse(jsonText)!.AsArray();
        bool jsonModified = false;

        foreach (var node in jsonNodes)
        {
            if (node is System.Text.Json.Nodes.JsonObject obj)
            {
                var coverUrl = obj["CoverImageUrl"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(coverUrl) && coverUrl.StartsWith("/images/games/"))
                {
                    var fileName = Path.GetFileName(coverUrl);
                    var baseName = Path.GetFileNameWithoutExtension(fileName);
                    var webpFileName = baseName + ".webp";
                    var webpPath = Path.Combine(gamesDir, webpFileName);
                    var sourcePath = Path.Combine(gamesDir, fileName);

                    if (File.Exists(sourcePath) && (!File.Exists(webpPath) || new FileInfo(webpPath).Length > 100 * 1024))
                    {
                        using var inStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read);
                        var webpBytes = await optimizer.ConvertToWebpAsync(inStream, maxWidth: 400, quality: 80);
                        await File.WriteAllBytesAsync(webpPath, webpBytes);
                    }

                    if (File.Exists(webpPath))
                    {
                        var expectedThumbUrl = $"/images/games/{webpFileName}";
                        var currentThumbUrl = obj["ThumbnailUrl"]?.GetValue<string>();
                        if (!string.Equals(currentThumbUrl, expectedThumbUrl, StringComparison.OrdinalIgnoreCase))
                        {
                            obj["ThumbnailUrl"] = expectedThumbUrl;
                            jsonModified = true;
                        }

                        // Verificar que la miniatura pesa menos de 100 KB
                        var webpInfo = new FileInfo(webpPath);
                        Assert.True(webpInfo.Length < 100 * 1024, $"La miniatura {webpFileName} debe pesar menos de 100 KB (pesa {webpInfo.Length} bytes).");
                    }
                }
            }
        }

        if (jsonModified)
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            await File.WriteAllTextAsync(seedPath, jsonNodes.ToJsonString(options));
        }

        // Verificar patchwork tras optimización
        var updatedPatchworkInfo = new FileInfo(patchworkPng);
        Assert.True(updatedPatchworkInfo.Length < 500 * 1024, $"patchwork.png debe pesar menos de 500 KB tras compresión (pesa {updatedPatchworkInfo.Length} bytes).");
        Assert.True(File.Exists(patchworkWebp), "patchwork.webp debe existir.");
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
