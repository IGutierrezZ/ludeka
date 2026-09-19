using System;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Xunit;

namespace Ludeka.UnitTests.Domain;

public class NotificationOutboxMessageTests
{
    [Fact]
    public void Constructor_WithValidArguments_InitializesCorrectly()
    {
        var message = new NotificationOutboxMessage(
            NotificationEventType.GiveawayExpiring,
            "Sorteo de Catán",
            "Quedan 24 horas para participar",
            "https://ludeka.es/sorteos/catan",
            "https://ludeka.es/img/catan.webp",
            "{\"premio\":\"Catán\"}",
            NotificationChannel.Discord);

        Assert.NotEqual(Guid.Empty, message.Id);
        Assert.Equal(NotificationEventType.GiveawayExpiring, message.EventType);
        Assert.Equal("Sorteo de Catán", message.Title);
        Assert.Equal("Quedan 24 horas para participar", message.Summary);
        Assert.Equal("https://ludeka.es/sorteos/catan", message.TargetUrl);
        Assert.Equal("https://ludeka.es/img/catan.webp", message.ImageUrl);
        Assert.Equal("{\"premio\":\"Catán\"}", message.FieldsJson);
        Assert.Equal(NotificationChannel.Discord, message.TargetChannel);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(0, message.Attempts);
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
        Assert.Throws<ArgumentException>(() => new NotificationOutboxMessage(
            NotificationEventType.CustomTestPing,
            title!,
            summary!));
    }

    [Fact]
    public void Constructor_WithSurroundingWhitespace_TrimsTitleSummaryAndUrls()
    {
        var message = new NotificationOutboxMessage(
            NotificationEventType.RuleQuestionAnswered,
            "  Pregunta respondida  ",
            "  El árbitro ha resuelto la duda  ",
            "  https://ludeka.es/reglas/12  ",
            "  https://ludeka.es/img/reglas.webp  ");

        Assert.Equal("Pregunta respondida", message.Title);
        Assert.Equal("El árbitro ha resuelto la duda", message.Summary);
        Assert.Equal("https://ludeka.es/reglas/12", message.TargetUrl);
        Assert.Equal("https://ludeka.es/img/reglas.webp", message.ImageUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankTargetUrl_CollapsesToNull(string targetUrl)
    {
        var message = new NotificationOutboxMessage(
            NotificationEventType.FridayReleasesSummary,
            "Novedades del viernes",
            "Resumen semanal",
            targetUrl);

        Assert.Null(message.TargetUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithBlankImageUrl_CollapsesToNull(string imageUrl)
    {
        var message = new NotificationOutboxMessage(
            NotificationEventType.FridayReleasesSummary,
            "Novedades del viernes",
            "Resumen semanal",
            targetUrl: null,
            imageUrl: imageUrl);

        Assert.Null(message.ImageUrl);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WithBlankOrNullFieldsJson_FallsBackToDefaultJson(string? fieldsJson)
    {
        var message = new NotificationOutboxMessage(
            NotificationEventType.FoundingVerdictPublished,
            "Nuevo veredicto",
            "Análisis oficial",
            fieldsJson: fieldsJson!);

        Assert.Equal("{}", message.FieldsJson);
    }

    [Fact]
    public void Constructor_WithTargetChannelOmitted_DefaultsToNullForBroadcast()
    {
        var message = new NotificationOutboxMessage(
            NotificationEventType.GiveawayExpiring,
            "Sorteo de Catán",
            "Quedan 24 horas para participar");

        Assert.Null(message.TargetChannel);
    }
}
