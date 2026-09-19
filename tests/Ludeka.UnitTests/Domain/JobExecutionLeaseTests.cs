using System;
using Ludeka.Core.Entities;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class JobExecutionLeaseTests
{
    [Fact]
    public void Constructor_WithValidArguments_InitializesRunningLease()
    {
        var lease = new JobExecutionLease("nightly-cataloging", "2026-09-18", "instance-a");

        Assert.NotEqual(Guid.Empty, lease.Id);
        Assert.Equal("nightly-cataloging", lease.JobName);
        Assert.Equal("2026-09-18", lease.WindowKey);
        Assert.Equal("instance-a", lease.HostIdentifier);
        Assert.Equal("Running", lease.Status);
        Assert.Null(lease.CompletedAt);
        Assert.Equal(0, lease.ProcessedCount);
        Assert.Equal(0, lease.FailedCount);
        Assert.Null(lease.DurationMs);
        Assert.Null(lease.ErrorMessage);
    }

    [Theory]
    [InlineData("", "2026-09-18")]
    [InlineData("   ", "2026-09-18")]
    [InlineData(null, "2026-09-18")]
    [InlineData("nightly-cataloging", "")]
    [InlineData("nightly-cataloging", "   ")]
    [InlineData("nightly-cataloging", null)]
    public void Constructor_WithBlankJobNameOrWindowKey_ThrowsArgumentException(string? jobName, string? windowKey)
    {
        Assert.Throws<ArgumentException>(() => new JobExecutionLease(jobName!, windowKey!));
    }

    [Fact]
    public void Constructor_WithoutHostIdentifier_DefaultsToNull()
    {
        var lease = new JobExecutionLease("price-radar", "2026-09-18T12");

        Assert.Null(lease.HostIdentifier);
    }

    [Fact]
    public void Touch_UpdatesHeartbeatAtToCurrentTime()
    {
        var lease = new JobExecutionLease("social-collector", "2026-09-18T14:00");

        var before = DateTimeOffset.UtcNow;
        lease.Touch();
        var after = DateTimeOffset.UtcNow;

        Assert.InRange(lease.HeartbeatAt, before, after);
    }

    [Fact]
    public void MarkCompleted_SetsTerminalCompletedStateWithMetrics()
    {
        var lease = new JobExecutionLease("nightly-cataloging", "2026-09-18");

        lease.MarkCompleted(processedCount: 42, failedCount: 1, durationMs: 1500);

        Assert.Equal("Completed", lease.Status);
        Assert.NotNull(lease.CompletedAt);
        Assert.Equal(42, lease.ProcessedCount);
        Assert.Equal(1, lease.FailedCount);
        Assert.Equal(1500, lease.DurationMs);
        Assert.Null(lease.ErrorMessage);
    }

    [Fact]
    public void MarkFailed_WithError_SetsTerminalFailedStateWithMetrics()
    {
        var lease = new JobExecutionLease("notification-outbox", "2026-09-18T14:03:07");

        lease.MarkFailed("Fallo de conexión con el proveedor.", processedCount: 5, failedCount: 3, durationMs: 800);

        Assert.Equal("Failed", lease.Status);
        Assert.NotNull(lease.CompletedAt);
        Assert.Equal(5, lease.ProcessedCount);
        Assert.Equal(3, lease.FailedCount);
        Assert.Equal(800, lease.DurationMs);
        Assert.Equal("Fallo de conexión con el proveedor.", lease.ErrorMessage);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MarkFailed_WithBlankOrNullError_AppliesDefaultMessage(string? error)
    {
        var lease = new JobExecutionLease("nightly-cataloging", "2026-09-18");

        lease.MarkFailed(error, processedCount: 0, failedCount: 0, durationMs: 0);

        Assert.Equal("Error no especificado durante la ejecución del trabajo.", lease.ErrorMessage);
    }
}
