using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.Entities;

/// <summary>
/// Cuenta o canal monitorizado de la comunidad lúdica (Instagram, YouTube, etc.)
/// perteneciente a una editorial, creador de contenido o tienda de referencia.
/// </summary>
public class MonitoredSocialAccount
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Name { get; private set; } = string.Empty;
    public SocialPlatform Platform { get; private set; } = SocialPlatform.Instagram;
    public string HandleOrChannelId { get; private set; } = string.Empty;
    public MonitoredAccountType AccountType { get; private set; } = MonitoredAccountType.Publisher;
    public string ProfileUrl { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; } = true;
    public DateTimeOffset? LastCheckedAt { get; private set; }
    public string? ResolvedFeedUrl { get; private set; }
    public string? Notes { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; private set; }

    // Constructor privado para EF Core
    private MonitoredSocialAccount() { }

    public MonitoredSocialAccount(
        string name,
        SocialPlatform platform,
        string handleOrChannelId,
        MonitoredAccountType accountType,
        string profileUrl,
        string? notes = null,
        bool isEnabled = true,
        Guid? id = null,
        DateTimeOffset? createdAt = null,
        string? resolvedFeedUrl = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la cuenta o canal no puede estar vacío.", nameof(name));

        if (string.IsNullOrWhiteSpace(handleOrChannelId))
            throw new ArgumentException("El identificador o handle no puede estar vacío.", nameof(handleOrChannelId));

        if (string.IsNullOrWhiteSpace(profileUrl))
            throw new ArgumentException("La URL del perfil no puede estar vacía.", nameof(profileUrl));

        Id = id ?? Guid.NewGuid();
        Name = name.Trim();
        Platform = platform;
        HandleOrChannelId = handleOrChannelId.Trim();
        AccountType = accountType;
        ProfileUrl = profileUrl.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        IsEnabled = isEnabled;
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow;
        ResolvedFeedUrl = string.IsNullOrWhiteSpace(resolvedFeedUrl) ? null : resolvedFeedUrl.Trim();
    }

    public void ToggleStatus(bool isEnabled)
    {
        IsEnabled = isEnabled;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateDetails(
        string name,
        SocialPlatform platform,
        string handleOrChannelId,
        MonitoredAccountType accountType,
        string profileUrl,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("El nombre de la cuenta o canal no puede estar vacío.", nameof(name));

        if (string.IsNullOrWhiteSpace(handleOrChannelId))
            throw new ArgumentException("El identificador o handle no puede estar vacío.", nameof(handleOrChannelId));

        if (string.IsNullOrWhiteSpace(profileUrl))
            throw new ArgumentException("La URL del perfil no puede estar vacía.", nameof(profileUrl));

        Name = name.Trim();
        Platform = platform;
        HandleOrChannelId = handleOrChannelId.Trim();
        AccountType = accountType;
        ProfileUrl = profileUrl.Trim();
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void SetResolvedFeedUrl(string? feedUrl)
    {
        ResolvedFeedUrl = string.IsNullOrWhiteSpace(feedUrl) ? null : feedUrl.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void MarkChecked()
    {
        LastCheckedAt = DateTimeOffset.UtcNow;
    }
}
