using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Ludeka.Infrastructure.Services;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class PhysicalFileImageStorageServiceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly PhysicalFileImageStorageService _service;

    public PhysicalFileImageStorageServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "ludeka_img_tests_" + Guid.NewGuid().ToString("N"));
        _service = new PhysicalFileImageStorageService(customPath: _tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch
            {
                // Ignorar errores de limpieza temporal
            }
        }
    }

    [Fact]
    public async Task SaveGameCoverAsync_ValidJpgFile_SavesFileAndReturnsRelativeUrl()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("fake-image-content-bytes");
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.SaveGameCoverAsync(
            slug: "catan-el-juego",
            contentStream: stream,
            originalFileName: "catan_cover.jpg",
            contentType: "image/jpeg"
        );

        // Assert
        Assert.True(result.Success);
        Assert.NotNull(result.RelativePath);
        Assert.StartsWith("/images/games/catan-el-juego-cover-", result.RelativePath);
        Assert.EndsWith(".jpg", result.RelativePath);

        // INC-48 (PR1a), diseño D1: con customPath, la portada debe escribirse bajo "{ruta}/games/",
        // coherente con la URL devuelta "/images/games/{fichero}". Defecto confirmado en
        // PhysicalFileImageStorageService.cs:28 (escribía directamente en "{ruta}", sin subcarpeta).
        var savedFile = Path.Combine(_tempDirectory, "games", Path.GetFileName(result.RelativePath));
        Assert.True(File.Exists(savedFile));
        Assert.False(File.Exists(Path.Combine(_tempDirectory, Path.GetFileName(result.RelativePath))));
    }

    [Fact]
    public async Task SaveGameCoverAsync_UnsupportedExtension_ReturnsError()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes("script content");
        using var stream = new MemoryStream(content);

        // Act
        var result = await _service.SaveGameCoverAsync(
            slug: "catan",
            contentStream: stream,
            originalFileName: "malicious.exe",
            contentType: "application/octet-stream"
        );

        // Assert
        Assert.False(result.Success);
        Assert.Contains("Formato no admitido", result.ErrorMessage);
    }

    [Fact]
    public async Task SaveGameCoverAsync_FileExceeds5Mb_ReturnsError()
    {
        // Arrange (5.5 MB mock stream)
        var stream = new FakeLargeStream(5500000);

        // Act
        var result = await _service.SaveGameCoverAsync(
            slug: "catan",
            contentStream: stream,
            originalFileName: "heavy.png",
            contentType: "image/png"
        );

        // Assert
        Assert.False(result.Success);
        Assert.Contains("excede el máximo permitido de 5 MB", result.ErrorMessage);
    }

    [Fact]
    public async Task ValidateCoverUrlAsync_ValidHttpAndHttps_ReturnsSuccess()
    {
        var res1 = await _service.ValidateCoverUrlAsync("https://cf.geekdo-images.com/sample.jpg");
        var res2 = await _service.ValidateCoverUrlAsync("http://example.com/cover.png");
        var res3 = await _service.ValidateCoverUrlAsync("/images/games/catan.jpg");

        Assert.True(res1.Success);
        Assert.True(res2.Success);
        Assert.True(res3.Success);
    }

    [Fact]
    public async Task ValidateCoverUrlAsync_InvalidUrl_ReturnsError()
    {
        var res1 = await _service.ValidateCoverUrlAsync("ftp://invalid.com/image.jpg");
        var res2 = await _service.ValidateCoverUrlAsync("");
        var res3 = await _service.ValidateCoverUrlAsync("just-some-text");

        Assert.False(res1.Success);
        Assert.False(res2.Success);
        Assert.False(res3.Success);
    }

    private class FakeLargeStream : MemoryStream
    {
        private readonly long _length;
        public FakeLargeStream(long length) => _length = length;
        public override long Length => _length;
    }
}
