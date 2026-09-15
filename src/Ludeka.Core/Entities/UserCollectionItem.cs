using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Entities;

public class UserCollectionItem
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string UserId { get; private set; } = string.Empty;
    public Guid? GameId { get; private set; }
    public CollectionStatus? Status { get; private set; }
    public bool IsPlayed { get; private set; }
    public DateTimeOffset AddedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Propiedades provisionales para títulos en cola de catalogación
    public int? BggId { get; private set; }
    public string? PendingTitle { get; private set; }
    public string? PendingThumbnailUrl { get; private set; }
    public int? PendingYearPublished { get; private set; }

    public bool IsPendingCataloging => GameId == null;

    // Navigation property for EF Core (opcional/lazy)
    public Game? Game { get; private set; }

    private UserCollectionItem() { }

    /// <summary>
    /// Constructor para juegos ya catalogados en Ludeka con estado de posesión/interés y estado de jugado.
    /// </summary>
    public UserCollectionItem(string userId, Guid gameId, CollectionStatus? status, bool isPlayed = false)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(userId));
        if (gameId == Guid.Empty)
            throw new ArgumentException("El identificador del juego no puede ser un GUID vacío.", nameof(gameId));
        if (!status.HasValue && !isPlayed)
            throw new InvalidOperationException("Un ítem de colección debe tener un estado de posesión o estar marcado como jugado.");

        UserId = userId.Trim();
        GameId = gameId;
        Status = status;
        IsPlayed = isPlayed || status == CollectionStatus.Played;
        AddedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Constructor de compatibilidad para juegos catalogados con estado no nulo.
    /// </summary>
    public UserCollectionItem(string userId, Guid gameId, CollectionStatus status)
        : this(userId, gameId, (CollectionStatus?)status, status == CollectionStatus.Played)
    {
    }

    /// <summary>
    /// Constructor para juegos importados de BGG que aún están en cola de auto-catalogación.
    /// </summary>
    public UserCollectionItem(
        string userId,
        int bggId,
        string pendingTitle,
        CollectionStatus? status,
        bool isPlayed = false,
        string? thumbnailUrl = null,
        int? yearPublished = null)
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("El identificador de usuario no puede estar vacío.", nameof(userId));
        if (bggId <= 0)
            throw new ArgumentException("El identificador BGG debe ser mayor a cero.", nameof(bggId));
        if (string.IsNullOrWhiteSpace(pendingTitle))
            throw new ArgumentException("El título provisional del juego no puede estar vacío.", nameof(pendingTitle));
        if (!status.HasValue && !isPlayed)
            throw new InvalidOperationException("Un ítem en cola debe tener un estado de posesión o estar marcado como jugado.");

        UserId = userId.Trim();
        GameId = null;
        BggId = bggId;
        PendingTitle = pendingTitle.Trim();
        Status = status;
        IsPlayed = isPlayed || status == CollectionStatus.Played;
        PendingThumbnailUrl = thumbnailUrl?.Trim();
        PendingYearPublished = yearPublished;
        AddedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Constructor de compatibilidad para cola BGG con CollectionStatus no nulo.
    /// </summary>
    public UserCollectionItem(
        string userId,
        int bggId,
        string pendingTitle,
        CollectionStatus status,
        string? thumbnailUrl = null,
        int? yearPublished = null)
        : this(userId, bggId, pendingTitle, (CollectionStatus?)status, status == CollectionStatus.Played, thumbnailUrl, yearPublished)
    {
    }

    public void ChangeStatus(CollectionStatus? newStatus)
    {
        if (newStatus == CollectionStatus.Played)
        {
            IsPlayed = true;
        }

        if (Status == newStatus) return;

        Status = newStatus;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetPlayed(bool isPlayed)
    {
        if (IsPlayed == isPlayed) return;

        IsPlayed = isPlayed;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void TogglePlayed()
    {
        SetPlayed(!IsPlayed);
    }

    /// <summary>
    /// Promociona el ítem a catalogado una vez que se ha creado la entidad Game en el catálogo.
    /// </summary>
    public void PromoteToCataloged(Guid gameId)
    {
        if (gameId == Guid.Empty)
            throw new ArgumentException("El identificador del juego catalogado no puede ser un GUID vacío.", nameof(gameId));

        GameId = gameId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Asocia la entidad Game para navegación y pruebas unitarias.
    /// </summary>
    public void AttachGame(Game game)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        GameId = game.Id;
    }
}
