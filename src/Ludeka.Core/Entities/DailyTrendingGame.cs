using System;

namespace Ludeka.Core.Entities;

/// <summary>
/// Representa la posición de un juego en la instantánea diaria del Top de tendencias mundiales (Hotness) de BoardGameGeek.
/// </summary>
public class DailyTrendingGame
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public DateOnly DateUtc { get; private set; }
    public int Rank { get; private set; }
    public int BggId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public int? YearPublished { get; private set; }
    public string? ThumbnailUrl { get; private set; }
    public Guid? GameId { get; private set; }
    public Game? Game { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    // Constructor protegido para EF Core
    protected DailyTrendingGame() { }

    public DailyTrendingGame(
        DateOnly dateUtc,
        int rank,
        int bggId,
        string title,
        int? yearPublished = null,
        string? thumbnailUrl = null,
        Guid? gameId = null,
        DateTimeOffset? createdAtUtc = null)
    {
        if (rank < 1 || rank > 50)
            throw new ArgumentOutOfRangeException(nameof(rank), "El puesto de tendencia debe estar comprendido entre 1 y 50.");
        if (bggId <= 0)
            throw new ArgumentOutOfRangeException(nameof(bggId), "El BGG ID debe ser positivo.");
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("El título del juego en tendencia no puede estar vacío.", nameof(title));

        DateUtc = dateUtc;
        Rank = rank;
        BggId = bggId;
        Title = title.Trim();
        YearPublished = yearPublished;
        ThumbnailUrl = string.IsNullOrWhiteSpace(thumbnailUrl) ? null : thumbnailUrl.Trim();
        GameId = gameId;
        CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow;
    }

    public void LinkToGame(Guid gameId)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("El identificador del juego no puede estar vacío.", nameof(gameId));
        GameId = gameId;
    }
}
