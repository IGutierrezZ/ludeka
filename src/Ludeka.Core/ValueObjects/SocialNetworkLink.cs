using System;
using Ludeka.Core.Enums;

namespace Ludeka.Core.ValueObjects;

/// <summary>
/// Enlace a un perfil, web o canal en una plataforma o red social.
/// </summary>
public record SocialNetworkLink
{
    public SocialPlatform Platform { get; init; }
    public string Url { get; init; } = string.Empty;
    public string? Handle { get; init; }
    public string? Title { get; init; }

    // Constructor sin parámetros para deserialización JSON / EF Core
    public SocialNetworkLink() { }

    public SocialNetworkLink(SocialPlatform platform, string url, string? handle = null, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("La URL del enlace social no puede estar vacía.", nameof(url));

        Platform = platform;
        Url = url.Trim();
        Handle = string.IsNullOrWhiteSpace(handle) ? null : handle.Trim();
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim();
    }

    /// <summary>
    /// Icono sugerido o emoji representativo de la red social.
    /// </summary>
    public string PlatformIcon => Platform switch
    {
        SocialPlatform.Website => "🌐",
        SocialPlatform.YouTube => "▶️",
        SocialPlatform.Instagram => "📷",
        SocialPlatform.Twitter => "🐦",
        SocialPlatform.Discord => "💬",
        SocialPlatform.Facebook => "📘",
        SocialPlatform.BoardGameGeek => "🎲",
        SocialPlatform.Twitch => "📺",
        SocialPlatform.TikTok => "📱",
        SocialPlatform.Telegram => "✈️",
        SocialPlatform.RssFeed => "📰",
        _ => "🔗"
    };

    /// <summary>
    /// Nombre amigable de la plataforma.
    /// </summary>
    public string PlatformName => Platform switch
    {
        SocialPlatform.Website => "Web oficial",
        SocialPlatform.YouTube => "YouTube",
        SocialPlatform.Instagram => "Instagram",
        SocialPlatform.Twitter => "X / Twitter",
        SocialPlatform.Discord => "Discord",
        SocialPlatform.Facebook => "Facebook",
        SocialPlatform.BoardGameGeek => "BoardGameGeek",
        SocialPlatform.Twitch => "Twitch",
        SocialPlatform.TikTok => "TikTok",
        SocialPlatform.Telegram => "Telegram",
        SocialPlatform.RssFeed => "Feed RSS / Blog",
        _ => "Enlace externo"
    };
}
