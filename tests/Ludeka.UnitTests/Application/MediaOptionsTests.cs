using System;
using System.IO;
using Ludeka.Application.Options;
using Xunit;

namespace Ludeka.UnitTests.Application;

/// <summary>
/// RED de la tarea extra 3.9 (INC-48, PR2, decisión del maintainer 2026-09-20): unifica en
/// <see cref="MediaOptions.ResolveLocalStoragePath"/> la resolución de ruta que hasta ahora vivía
/// duplicada en dos métodos <c>private</c> sin ninguna prueba que los cruzara —
/// <c>LudekaServiceCollectionExtensions.ResolveMediaStoragePath</c> (Ludeka.Infrastructure, ruta de
/// escritura) y <c>MediaStaticFilesExtensions.ResolveMediaRoot</c> (Ludeka.Web, ruta de lectura
/// servida por HTTP). Semántica exacta preservada: ruta absoluta → tal cual; relativa →
/// <c>Path.Combine(contentRootPath, ruta)</c>; sin ruta local configurada → <see langword="null"/>;
/// <c>contentRootPath</c> nulo/vacío con ruta relativa → la ruta relativa tal cual.
/// </summary>
public class MediaOptionsTests
{
    [Fact]
    public void ResolveLocalStoragePath_SinRutaLocalConfigurada_DevuelveNull()
    {
        var options = new MediaOptions { LocalStoragePath = "" };

        var resolved = options.ResolveLocalStoragePath(contentRootPath: Path.GetTempPath());

        Assert.Null(resolved);
    }

    [Fact]
    public void ResolveLocalStoragePath_ConRutaAbsoluta_DevuelveLaRutaTalCualIgnorandoContentRootPath()
    {
        var absolutePath = Path.Combine(Path.GetTempPath(), $"ludeka-media-{Guid.NewGuid():N}");
        var options = new MediaOptions { LocalStoragePath = absolutePath };

        var resolved = options.ResolveLocalStoragePath(
            contentRootPath: Path.Combine(Path.GetTempPath(), "otro-content-root-distinto"));

        Assert.Equal(absolutePath, resolved);
    }

    [Fact]
    public void ResolveLocalStoragePath_ConRutaRelativaYContentRootPath_CombinaAmbas()
    {
        var options = new MediaOptions { LocalStoragePath = "media-local" };
        var contentRootPath = Path.GetTempPath();

        var resolved = options.ResolveLocalStoragePath(contentRootPath);

        Assert.Equal(Path.Combine(contentRootPath, "media-local"), resolved);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveLocalStoragePath_ConContentRootPathVacioONulo_DevuelveLaRutaRelativaTalCual(string? contentRootPath)
    {
        var options = new MediaOptions { LocalStoragePath = "media-local" };

        var resolved = options.ResolveLocalStoragePath(contentRootPath);

        Assert.Equal("media-local", resolved);
    }
}
