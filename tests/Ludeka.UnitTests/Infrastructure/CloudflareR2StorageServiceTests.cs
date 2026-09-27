using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Application.Options;
using Ludeka.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Ludeka.UnitTests.Infrastructure;

public class CloudflareR2StorageServiceTests
{
    private readonly CloudflareR2Options _defaultOptions = new()
    {
        AccountId = "test-account",
        AccessKeyId = "test-key-id",
        SecretAccessKey = "test-secret-key",
        BucketName = "ludeka-media",
        PublicCdnBaseUrl = "https://cdn.ludeka.es"
    };

    [Fact]
    public async Task UploadOptimizedImageAsync_ValidStreamAndKey_CallsOptimizationAndUploadsToS3WithHeaders()
    {
        // Arrange
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake-image-bytes"));
        var objectKey = "covers/catan.webp";

        // Act
        var resultUrl = await service.UploadOptimizedImageAsync(stream, objectKey, maxWidth: 800, quality: 90);

        // Assert
        Assert.Equal("https://cdn.ludeka.es/covers/catan.webp", resultUrl);
        Assert.Single(fakeS3.PutRequests);

        var putRequest = fakeS3.PutRequests[0];
        Assert.Equal("ludeka-media", putRequest.BucketName);
        Assert.Equal("covers/catan.webp", putRequest.Key);
        Assert.Equal("image/webp", putRequest.ContentType);
        Assert.Equal("public, max-age=31536000, immutable", putRequest.Headers.CacheControl);
        Assert.True(putRequest.DisablePayloadSigning);
        Assert.True(putRequest.DisableDefaultChecksumValidation);

        Assert.Single(fakeOptimizer.ConversionCalls);
        Assert.Equal(800, fakeOptimizer.ConversionCalls[0].MaxWidth);
        Assert.Equal(90, fakeOptimizer.ConversionCalls[0].Quality);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UploadOptimizedImageAsync_InvalidKey_ThrowsArgumentException(string? invalidKey)
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAnyAsync<ArgumentException>(() =>
            service.UploadOptimizedImageAsync(stream, invalidKey!));
    }

    [Fact]
    public async Task UploadOptimizedImageAsync_NullStream_ThrowsArgumentNullException()
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.UploadOptimizedImageAsync(null!, "valid/key.webp"));
    }

    [Theory]
    [InlineData("cover")]
    [InlineData("boxartfront")]
    public async Task UploadGameImageVariantsAsync_CoverType_UploadsBothFullAndThumbVariants(string imageType)
    {
        // Arrange
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample-image-stream"));

        // Act
        var result = await service.UploadGameImageVariantsAsync(stream, 13, imageType);

        // Assert
        Assert.Equal("https://cdn.ludeka.es/games/13/cover.webp", result.FullUrl);
        Assert.Equal("https://cdn.ludeka.es/games/13/cover_thumb.webp", result.ThumbUrl);
        Assert.Equal(2, fakeS3.PutRequests.Count);

        var fullReq = fakeS3.PutRequests.Find(r => r.Key == "games/13/cover.webp");
        var thumbReq = fakeS3.PutRequests.Find(r => r.Key == "games/13/cover_thumb.webp");

        Assert.NotNull(fullReq);
        Assert.NotNull(thumbReq);

        Assert.Equal(2, fakeOptimizer.ConversionCalls.Count);
        var fullOpt = fakeOptimizer.ConversionCalls.Find(c => c.MaxWidth == 1000);
        var thumbOpt = fakeOptimizer.ConversionCalls.Find(c => c.MaxWidth == 400);

        Assert.NotNull(fullOpt);
        Assert.NotNull(thumbOpt);
        Assert.Equal(82, fullOpt.Quality);
        Assert.Equal(80, thumbOpt.Quality);
    }

    [Theory]
    [InlineData("back")]
    [InlineData("boxartback")]
    [InlineData("boxback")]
    public async Task UploadGameImageVariantsAsync_BackType_UploadsSingleVariant(string imageType)
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample-image-stream"));

        var result = await service.UploadGameImageVariantsAsync(stream, 13, imageType);

        Assert.Equal("https://cdn.ludeka.es/games/13/back.webp", result.FullUrl);
        Assert.Equal("https://cdn.ludeka.es/games/13/back.webp", result.ThumbUrl);
        Assert.Single(fakeS3.PutRequests);
        Assert.Equal("games/13/back.webp", fakeS3.PutRequests[0].Key);
    }

    [Theory]
    [InlineData("table")]
    [InlineData("gameplay")]
    [InlineData("components")]
    public async Task UploadGameImageVariantsAsync_TableType_Uploads1200WidthVariant(string imageType)
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample-image-stream"));

        var result = await service.UploadGameImageVariantsAsync(stream, 13, imageType);

        Assert.Equal("https://cdn.ludeka.es/games/13/table.webp", result.FullUrl);
        Assert.Single(fakeS3.PutRequests);
        Assert.Equal("games/13/table.webp", fakeS3.PutRequests[0].Key);

        Assert.Single(fakeOptimizer.ConversionCalls);
        Assert.Equal(1200, fakeOptimizer.ConversionCalls[0].MaxWidth);
    }

    [Fact]
    public async Task UploadGameImageVariantsAsync_CustomType_UploadsCustomKey()
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample-image-stream"));

        var result = await service.UploadGameImageVariantsAsync(stream, 13, "PromoArt");

        Assert.Equal("https://cdn.ludeka.es/games/13/promoart.webp", result.FullUrl);
        Assert.Single(fakeS3.PutRequests);
        Assert.Equal("games/13/promoart.webp", fakeS3.PutRequests[0].Key);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task UploadGameImageVariantsAsync_InvalidBggId_ThrowsArgumentOutOfRangeException(int bggId)
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        using var stream = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.UploadGameImageVariantsAsync(stream, bggId, "cover"));
    }

    [Fact]
    public async Task DeleteImageAsync_ValidKey_DeletesFromS3AndReturnsTrue()
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        var result = await service.DeleteImageAsync("games/13/cover.webp");

        Assert.True(result);
        Assert.Single(fakeS3.DeletedKeys);
        Assert.Equal("games/13/cover.webp", fakeS3.DeletedKeys[0]);
    }

    [Fact]
    public async Task DeleteImageAsync_WhenS3Throws_ReturnsFalseGracefully()
    {
        var fakeS3 = new FakeAmazonS3Client { ShouldThrowOnDelete = true };
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        var result = await service.DeleteImageAsync("games/13/cover.webp");

        Assert.False(result);
    }

    [Theory]
    [InlineData("covers/game.webp", "https://cdn.ludeka.es/covers/game.webp")]
    [InlineData("/covers/game.webp", "https://cdn.ludeka.es/covers/game.webp")]
    public void GetPublicUrl_HandlesLeadingAndTrailingSlashes(string key, string expectedUrl)
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(new CloudflareR2Options
        {
            AccountId = "acc",
            AccessKeyId = "k",
            SecretAccessKey = "s",
            BucketName = "b",
            PublicCdnBaseUrl = "https://cdn.ludeka.es/"
        });
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        var url = service.GetPublicUrl(key);

        Assert.Equal(expectedUrl, url);
    }

    [Fact]
    public async Task SaveGameCoverAsync_SavesWithTimestampedKey()
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var result = await service.SaveGameCoverAsync("catan", stream, "cover.jpg", "image/jpeg");

        Assert.True(result.Success);
        Assert.NotNull(result.RelativePath);
        Assert.Contains("games/catan/cover-", result.RelativePath);
        Assert.EndsWith(".webp", result.RelativePath);
    }

    [Fact]
    public async Task SaveEventPosterAsync_SavesWithTimestampedKey()
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var result = await service.SaveEventPosterAsync("interocio-2026", stream, "poster.jpg", "image/jpeg");

        Assert.True(result.Success);
        Assert.NotNull(result.RelativePath);
        Assert.Contains("events/interocio-2026-", result.RelativePath);
        Assert.EndsWith(".webp", result.RelativePath);
    }

    [Fact]
    public async Task SaveCommunityImageAsync_SavesInCorrectFolder()
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var result = await service.SaveCommunityImageAsync("avatar", "user-123", stream, "avatar.png", "image/png");

        Assert.True(result.Success);
        Assert.NotNull(result.RelativePath);
        Assert.Contains("avatar/user-123-", result.RelativePath);
    }

    [Theory]
    [InlineData("https://ejemplo.com/cover.jpg", true)]
    [InlineData("http://ejemplo.com/cover.jpg", true)]
    [InlineData("", false)]
    [InlineData("not-a-valid-url", false)]
    [InlineData("ftp://ejemplo.com/cover.jpg", false)]
    public async Task ValidateCoverUrlAsync_ValidatesUrlSchemes(string url, bool expectedSuccess)
    {
        var fakeS3 = new FakeAmazonS3Client();
        var fakeOptimizer = new FakeImageOptimizationService();
        var options = Options.Create(_defaultOptions);
        var service = new CloudflareR2StorageService(fakeOptimizer, options, NullLogger<CloudflareR2StorageService>.Instance, fakeS3);

        var result = await service.ValidateCoverUrlAsync(url);

        Assert.Equal(expectedSuccess, result.Success);
    }

    private class FakeAmazonS3Client : AmazonS3Client
    {
        public List<PutObjectRequest> PutRequests { get; } = [];
        public List<string> DeletedKeys { get; } = [];
        public bool ShouldThrowOnDelete { get; set; }

        public FakeAmazonS3Client()
            : base(new AnonymousAWSCredentials(), new AmazonS3Config { ServiceURL = "http://localhost" })
        {
        }

        public override Task<PutObjectResponse> PutObjectAsync(PutObjectRequest request, CancellationToken cancellationToken = default)
        {
            PutRequests.Add(request);
            return Task.FromResult(new PutObjectResponse
            {
                HttpStatusCode = HttpStatusCode.OK
            });
        }

        public override Task<DeleteObjectResponse> DeleteObjectAsync(string bucketName, string key, CancellationToken cancellationToken = default)
        {
            if (ShouldThrowOnDelete)
            {
                throw new AmazonS3Exception("Simulated R2 connection error");
            }

            DeletedKeys.Add(key);
            return Task.FromResult(new DeleteObjectResponse
            {
                HttpStatusCode = HttpStatusCode.NoContent
            });
        }
    }

    private class FakeImageOptimizationService : IImageOptimizationService
    {
        public record ConversionCall(int MaxWidth, int Quality);
        public List<ConversionCall> ConversionCalls { get; } = [];

        public Task<byte[]> ConvertToWebpAsync(Stream inputStream, int maxWidth = 1000, int quality = 82, CancellationToken ct = default)
        {
            ConversionCalls.Add(new ConversionCall(maxWidth, quality));
            return Task.FromResult(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // Dummy WebP header
        }

        public Task<ImageDimensions> GetDimensionsAsync(Stream inputStream, CancellationToken ct = default)
        {
            return Task.FromResult(new ImageDimensions(1000, 1000));
        }
    }
}
