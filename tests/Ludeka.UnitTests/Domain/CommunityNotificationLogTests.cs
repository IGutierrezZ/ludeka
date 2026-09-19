using System;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class CommunityNotificationLogTests
{
    [Fact]
    public void Constructor_WithValidArguments_InitializesCorrectly()
    {
        var log = new CommunityNotificationLog(
            NotificationEventType.GiveawayExpiring,
            NotificationChannel.Discord,
            "Alerta de Sorteo",
            "Finaliza en 24h",
            "https://ludeka.es/radar",
            "https://ludeka.es/img.webp",
            NotificationStatus.Queued);

        Assert.NotEqual(Guid.Empty, log.Id);
        Assert.Equal(NotificationEventType.GiveawayExpiring, log.EventType);
        Assert.Equal(NotificationChannel.Discord, log.Channel);
        Assert.Equal("Alerta de Sorteo", log.Title);
        Assert.Equal("Finaliza en 24h", log.Summary);
        Assert.Equal("https://ludeka.es/radar", log.TargetUrl);
        Assert.Equal("https://ludeka.es/img.webp", log.ImageUrl);
        Assert.Equal(NotificationStatus.Queued, log.Status);
        Assert.Null(log.SentAt);
        Assert.Null(log.ErrorDetails);
    }

    [Theory]
    [InlineData("", "Resumen válido")]
    [InlineData("   ", "Resumen válido")]
    [InlineData(null, "Resumen válido")]
    [InlineData("Título válido", "")]
    [InlineData("Título válido", "   ")]
    [InlineData("Título válido", null)]
    public void Constructor_WithInvalidTitleOrSummary_ThrowsArgumentException(string? title, string? summary)
    {
        Assert.Throws<ArgumentException>(() => new CommunityNotificationLog(
            NotificationEventType.CustomTestPing,
            NotificationChannel.Telegram,
            title!,
            summary!));
    }

    [Fact]
    public void MarkAsSent_UpdatesStatusAndSentAt()
    {
        var log = new CommunityNotificationLog(
            NotificationEventType.FridayReleasesSummary,
            NotificationChannel.Telegram,
            "Novedades",
            "Resumen del viernes");

        log.MarkAsSent();

        Assert.Equal(NotificationStatus.Sent, log.Status);
        Assert.NotNull(log.SentAt);
        Assert.Null(log.ErrorDetails);
    }

    [Fact]
    public void MarkAsFailed_UpdatesStatusAndErrorDetails()
    {
        var log = new CommunityNotificationLog(
            NotificationEventType.FoundingVerdictPublished,
            NotificationChannel.Discord,
            "Nuevo Veredicto",
            "Análisis oficial");

        log.MarkAsFailed("HTTP 429 Rate Limit Exceeded");

        Assert.Equal(NotificationStatus.Failed, log.Status);
        Assert.Equal("HTTP 429 Rate Limit Exceeded", log.ErrorDetails);
    }

    [Fact]
    public void MarkAsDryRun_UpdatesStatusToDryRun()
    {
        var log = new CommunityNotificationLog(
            NotificationEventType.CustomTestPing,
            NotificationChannel.Discord,
            "Ping de prueba",
            "Simulación local");

        log.MarkAsDryRun();

        Assert.Equal(NotificationStatus.DryRun, log.Status);
        Assert.NotNull(log.SentAt);
        Assert.Null(log.ErrorDetails);
    }

    [Fact]
    public void ForDelivery_WithValidArguments_PopulatesMessageIdAndQueuesStatus()
    {
        var messageId = Guid.NewGuid();

        var log = CommunityNotificationLog.ForDelivery(
            messageId,
            NotificationEventType.GiveawayExpiring,
            NotificationChannel.Discord,
            "Sorteo de Catán",
            "Quedan 24 horas para participar",
            "https://ludeka.es/sorteos/catan",
            "https://ludeka.es/img/catan.webp");

        Assert.Equal(messageId, log.MessageId);
        Assert.Equal(NotificationStatus.Queued, log.Status);
        Assert.Equal(0, log.Attempts);
        Assert.Equal(NotificationEventType.GiveawayExpiring, log.EventType);
        Assert.Equal(NotificationChannel.Discord, log.Channel);
        Assert.Equal("Sorteo de Catán", log.Title);
        Assert.Equal("Quedan 24 horas para participar", log.Summary);
    }

    [Fact]
    public void RegisterFailedAttempt_WithFirstFailure_IncrementsAttemptsAndSchedulesRetryWithoutTerminalStatus()
    {
        var log = CommunityNotificationLog.ForDelivery(
            Guid.NewGuid(),
            NotificationEventType.FridayReleasesSummary,
            NotificationChannel.Telegram,
            "Novedades",
            "Resumen del viernes",
            null,
            null);
        var nextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(5);

        log.RegisterFailedAttempt("HTTP 429 Rate Limit Exceeded", nextAttemptAt);

        Assert.Equal(1, log.Attempts);
        Assert.Equal(nextAttemptAt, log.NextAttemptAt);
        Assert.Equal(NotificationStatus.Queued, log.Status);
        Assert.Equal("HTTP 429 Rate Limit Exceeded", log.ErrorDetails);
    }

    [Fact]
    public void RegisterFailedAttempt_CalledTwice_AccumulatesAttempts()
    {
        var log = CommunityNotificationLog.ForDelivery(
            Guid.NewGuid(),
            NotificationEventType.FridayReleasesSummary,
            NotificationChannel.Telegram,
            "Novedades",
            "Resumen del viernes",
            null,
            null);

        log.RegisterFailedAttempt("Error de red", DateTimeOffset.UtcNow.AddMinutes(1));
        log.RegisterFailedAttempt("Error de red", DateTimeOffset.UtcNow.AddMinutes(4));

        Assert.Equal(2, log.Attempts);
        Assert.Equal(NotificationStatus.Queued, log.Status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void RegisterFailedAttempt_WithBlankOrNullError_AppliesDefaultMessage(string? error)
    {
        var log = CommunityNotificationLog.ForDelivery(
            Guid.NewGuid(),
            NotificationEventType.FridayReleasesSummary,
            NotificationChannel.Telegram,
            "Novedades",
            "Resumen del viernes",
            null,
            null);

        log.RegisterFailedAttempt(error!, DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal("Error no especificado al reintentar la notificación.", log.ErrorDetails);
    }

    [Fact]
    public void MarkAsPermanentlyFailed_WithError_SetsTerminalFailedStatus()
    {
        var log = CommunityNotificationLog.ForDelivery(
            Guid.NewGuid(),
            NotificationEventType.FoundingVerdictPublished,
            NotificationChannel.Discord,
            "Nuevo Veredicto",
            "Análisis oficial",
            null,
            null);
        log.RegisterFailedAttempt("Primer intento fallido", DateTimeOffset.UtcNow.AddMinutes(1));

        log.MarkAsPermanentlyFailed("Se agotaron los reintentos");

        Assert.Equal(NotificationStatus.Failed, log.Status);
        Assert.Equal("Se agotaron los reintentos", log.ErrorDetails);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void MarkAsPermanentlyFailed_WithBlankOrNullError_AppliesDefaultMessage(string? error)
    {
        var log = CommunityNotificationLog.ForDelivery(
            Guid.NewGuid(),
            NotificationEventType.FoundingVerdictPublished,
            NotificationChannel.Discord,
            "Nuevo Veredicto",
            "Análisis oficial",
            null,
            null);

        log.MarkAsPermanentlyFailed(error!);

        Assert.Equal("Error no especificado al agotar los intentos de entrega.", log.ErrorDetails);
        Assert.Equal(NotificationStatus.Failed, log.Status);
    }
}
