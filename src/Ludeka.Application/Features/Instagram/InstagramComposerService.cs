using System;
using System.Globalization;
using System.Security;
using System.Text;
using Ludeka.Application.Contracts;
using Ludeka.Application.DTOs;
using Ludeka.Core.Entities;
using Ludeka.Core.Enums;
using Ludeka.Core.ValueObjects;

namespace Ludeka.Application.Features.Instagram;

public class InstagramComposerService : IInstagramComposerService
{
    private readonly ISocialCardService _socialCardService;

    public InstagramComposerService(ISocialCardService socialCardService)
    {
        _socialCardService = socialCardService ?? throw new ArgumentNullException(nameof(socialCardService));
    }

    public string ComposeSvg(InstagramPostSourceType sourceType, object sourceEntity, string theme = "Dark")
    {
        ArgumentNullException.ThrowIfNull(sourceEntity);

        var isDark = !string.Equals(theme, "Light", StringComparison.OrdinalIgnoreCase);

        return sourceType switch
        {
            InstagramPostSourceType.Giveaway when sourceEntity is Giveaway g => ComposeGiveawaySvg(g, isDark),
            InstagramPostSourceType.WeeklyRelease when sourceEntity is WeeklyRelease r => ComposeReleaseSvg(r, isDark),
            InstagramPostSourceType.Game when sourceEntity is Game game => ComposeGameSvg(game, isDark),
            _ when sourceEntity is SocialCardDataDto data => _socialCardService.GenerateSvg(data),
            _ => ComposeGenericSvg(sourceEntity.ToString() ?? "Ludeka", isDark)
        };
    }

    public string GenerateCaption(InstagramPostSourceType sourceType, object sourceEntity)
    {
        ArgumentNullException.ThrowIfNull(sourceEntity);

        return sourceType switch
        {
            InstagramPostSourceType.Giveaway when sourceEntity is Giveaway g => GenerateGiveawayCaption(g),
            InstagramPostSourceType.WeeklyRelease when sourceEntity is WeeklyRelease r => GenerateReleaseCaption(r),
            InstagramPostSourceType.Game when sourceEntity is Game game => GenerateGameCaption(game),
            _ when sourceEntity is SocialCardDataDto data => _socialCardService.GenerateInstagramCaption(data),
            _ => "🎲 ¡Descubre las últimas novedades y sorteos de juegos de mesa en ludeka.app!\n\n#juegosdemesa #ludeka"
        };
    }

    private static string ComposeGiveawaySvg(Giveaway g, bool isDark)
    {
        var title = EscapeXml(g.Title);
        var organizer = EscapeXml(g.FormattedOrganizer);
        var gameTitle = !string.IsNullOrWhiteSpace(g.GameTitle) ? EscapeXml(g.GameTitle) : null;
        var country = EscapeXml(g.IsInternational ? "Internacional 🌎" : $"{g.Country} {CountryCatalog.GetFlag(g.Country)}");
        var deadlineStr = g.DeadlineAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        var coverUrl = !string.IsNullOrWhiteSpace(g.ThumbnailUrl) ? EscapeXml(g.ThumbnailUrl) : string.Empty;

        var bgColor1 = isDark ? "#0B0F17" : "#FFFFFF";
        var bgColor2 = isDark ? "#131B2A" : "#F8FAFC";
        var textPrimary = isDark ? "#FFFFFF" : "#0F172A";
        var textSecondary = isDark ? "#94A3B8" : "#475569";
        var cardBorder = isDark ? "#1E293B" : "#E2E8F0";

        var sb = new StringBuilder();
        sb.AppendLine(@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 1080 1080"" width=""1080"" height=""1080"">");
        sb.AppendLine(@"  <defs>");
        sb.AppendLine($@"    <linearGradient id=""bgGrad"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">");
        sb.AppendLine($@"      <stop offset=""0%"" stop-color=""{bgColor1}""/>");
        sb.AppendLine($@"      <stop offset=""100%"" stop-color=""{bgColor2}""/>");
        sb.AppendLine(@"    </linearGradient>");
        sb.AppendLine(@"    <clipPath id=""coverClip"">");
        sb.AppendLine(@"      <rect x=""140"" y=""120"" width=""800"" height=""450"" rx=""24""/>");
        sb.AppendLine(@"    </clipPath>");
        sb.AppendLine(@"  </defs>");

        // Fondo
        sb.AppendLine(@"  <rect width=""1080"" height=""1080"" fill=""url(#bgGrad)""/>");
        sb.AppendLine(@"  <circle cx=""540"" cy=""120"" r=""380"" fill=""#F97316"" opacity=""0.08""/>");

        // Cabecera de Marca
        sb.AppendLine(@"  <g transform=""translate(140, 64)"">");
        sb.AppendLine($@"    <text x=""0"" y=""28"" fill=""{textPrimary}"" font-family=""'Inter', sans-serif"" font-size=""32"" font-weight=""900"">Ludeka<tspan fill=""#F97316"">.</tspan></text>");
        sb.AppendLine($@"    <text x=""140"" y=""26"" fill=""{textSecondary}"" font-family=""'Inter', sans-serif"" font-size=""16"" font-weight=""700"" letter-spacing=""0.5"">RADAR DE SORTEOS OFICIALES</text>");
        sb.AppendLine(@"  </g>");

        // Carátula / Imagen
        sb.AppendLine($@"  <rect x=""140"" y=""120"" width=""800"" height=""450"" rx=""24"" fill=""{(isDark ? "#0F172A" : "#E2E8F0")}"" stroke=""{cardBorder}"" stroke-width=""2""/>");
        if (!string.IsNullOrWhiteSpace(coverUrl))
        {
            sb.AppendLine($@"  <image href=""{coverUrl}"" x=""140"" y=""120"" width=""800"" height=""450"" preserveAspectRatio=""xMidYMid slice"" clip-path=""url(#coverClip)""/>");
        }

        // Píldoras sobre la imagen
        sb.AppendLine(@"  <g transform=""translate(170, 150)"">");
        sb.AppendLine(@"    <rect x=""0"" y=""0"" width=""180"" height=""40"" rx=""20"" fill=""#EA580C"" fill-opacity=""0.95""/>");
        sb.AppendLine(@"    <text x=""90"" y=""26"" fill=""#FFFFFF"" font-family=""'Inter', sans-serif"" font-size=""15"" font-weight=""800"" text-anchor=""middle"">🎁 Sorteo Activo</text>");
        sb.AppendLine(@"  </g>");

        sb.AppendLine(@"  <g transform=""translate(365, 150)"">");
        sb.AppendLine(@"    <rect x=""0"" y=""0"" width=""180"" height=""40"" rx=""20"" fill=""#0F172A"" fill-opacity=""0.85"" stroke=""#334155"" stroke-width=""1""/>");
        sb.AppendLine($@"    <text x=""90"" y=""25"" fill=""#E2E8F0"" font-family=""'Inter', sans-serif"" font-size=""14"" font-weight=""700"" text-anchor=""middle"">{country}</text>");
        sb.AppendLine(@"  </g>");

        // Cuerpo: Título y Organización
        sb.AppendLine(@"  <g transform=""translate(140, 620)"">");
        sb.AppendLine($@"    <text x=""0"" y=""40"" fill=""{textPrimary}"" font-family=""'Inter', sans-serif"" font-size=""40"" font-weight=""900"">{Truncate(title, 38)}</text>");
        sb.AppendLine($@"    <text x=""0"" y=""80"" fill=""{textSecondary}"" font-family=""'Inter', sans-serif"" font-size=""22"" font-weight=""600"">Organizado por: {organizer}</text>");
        if (!string.IsNullOrWhiteSpace(gameTitle))
        {
            sb.AppendLine($@"    <text x=""0"" y=""115"" fill=""#F97316"" font-family=""'Inter', sans-serif"" font-size=""18"" font-weight=""700"">🎲 Juego: {gameTitle}</text>");
        }
        sb.AppendLine(@"  </g>");

        // Píldora de Fecha Límite
        sb.AppendLine(@"  <g transform=""translate(140, 775)"">");
        sb.AppendLine($@"    <rect x=""0"" y=""0"" width=""480"" height=""52"" rx=""16"" fill=""{(isDark ? "#1E293B" : "#F1F5F9")}"" stroke=""{cardBorder}"" stroke-width=""1.5""/>");
        sb.AppendLine($@"    <text x=""24"" y=""33"" fill=""{textPrimary}"" font-family=""'Inter', sans-serif"" font-size=""17"" font-weight=""800"">⏳ Límite: {deadlineStr}</text>");
        sb.AppendLine(@"  </g>");

        // Pie de Marca
        sb.AppendLine(@"  <g transform=""translate(140, 880)"">");
        sb.AppendLine($@"    <rect x=""0"" y=""0"" width=""800"" height=""100"" rx=""20"" fill=""{(isDark ? "#0F172A" : "#F8FAFC")}"" stroke=""{cardBorder}"" stroke-width=""1""/>");
        sb.AppendLine(@"    <text x=""40"" y=""42"" fill=""#F97316"" font-family=""'Inter', sans-serif"" font-size=""18"" font-weight=""800"">LUDEKA &bull; RADAR LÚDICO</text>");
        sb.AppendLine($@"    <text x=""40"" y=""72"" fill=""{textPrimary}"" font-family=""'Inter', sans-serif"" font-size=""20"" font-weight=""700"">Participa y consulta enlaces oficiales en ludeka.app</text>");
        sb.AppendLine(@"  </g>");

        sb.AppendLine(@"</svg>");
        return sb.ToString();
    }

    private static string ComposeReleaseSvg(WeeklyRelease r, bool isDark)
    {
        var title = EscapeXml(r.Title);
        var publisher = EscapeXml(r.Publisher);
        var releaseDateStr = r.ReleaseDate.HasValue
            ? r.ReleaseDate.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : "Próximamente";
        var pvpStr = r.EstimatedPvp.HasValue ? $"{r.EstimatedPvp.Value:0.00} €" : "PVP por confirmar";
        var coverUrl = !string.IsNullOrWhiteSpace(r.CoverImageUrl) ? EscapeXml(r.CoverImageUrl) : string.Empty;

        var bgColor1 = isDark ? "#0B0F17" : "#FFFFFF";
        var bgColor2 = isDark ? "#131B2A" : "#F8FAFC";
        var textPrimary = isDark ? "#FFFFFF" : "#0F172A";
        var textSecondary = isDark ? "#94A3B8" : "#475569";
        var cardBorder = isDark ? "#1E293B" : "#E2E8F0";

        var sb = new StringBuilder();
        sb.AppendLine(@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 1080 1080"" width=""1080"" height=""1080"">");
        sb.AppendLine(@"  <defs>");
        sb.AppendLine($@"    <linearGradient id=""bgGrad"" x1=""0%"" y1=""0%"" x2=""100%"" y2=""100%"">");
        sb.AppendLine($@"      <stop offset=""0%"" stop-color=""{bgColor1}""/>");
        sb.AppendLine($@"      <stop offset=""100%"" stop-color=""{bgColor2}""/>");
        sb.AppendLine(@"    </linearGradient>");
        sb.AppendLine(@"    <clipPath id=""coverClip"">");
        sb.AppendLine(@"      <rect x=""140"" y=""120"" width=""800"" height=""450"" rx=""24""/>");
        sb.AppendLine(@"    </clipPath>");
        sb.AppendLine(@"  </defs>");

        sb.AppendLine(@"  <rect width=""1080"" height=""1080"" fill=""url(#bgGrad)""/>");
        sb.AppendLine(@"  <circle cx=""540"" cy=""120"" r=""380"" fill=""#3B82F6"" opacity=""0.08""/>");

        // Cabecera
        sb.AppendLine(@"  <g transform=""translate(140, 64)"">");
        sb.AppendLine($@"    <text x=""0"" y=""28"" fill=""{textPrimary}"" font-family=""'Inter', sans-serif"" font-size=""32"" font-weight=""900"">Ludeka<tspan fill=""#F97316"">.</tspan></text>");
        sb.AppendLine($@"    <text x=""140"" y=""26"" fill=""{textSecondary}"" font-family=""'Inter', sans-serif"" font-size=""16"" font-weight=""700"" letter-spacing=""0.5"">NOVEDADES Y LANZAMIENTOS DE TIENDAS</text>");
        sb.AppendLine(@"  </g>");

        // Carátula
        sb.AppendLine($@"  <rect x=""140"" y=""120"" width=""800"" height=""450"" rx=""24"" fill=""{(isDark ? "#0F172A" : "#E2E8F0")}"" stroke=""{cardBorder}"" stroke-width=""2""/>");
        if (!string.IsNullOrWhiteSpace(coverUrl))
        {
            sb.AppendLine($@"  <image href=""{coverUrl}"" x=""140"" y=""120"" width=""800"" height=""450"" preserveAspectRatio=""xMidYMid slice"" clip-path=""url(#coverClip)""/>");
        }

        // Píldoras
        var badgeText = r.IsReprint ? "🔄 Reimpresión" : "📰 Novedad Editorial";
        var badgeBg = r.IsReprint ? "#059669" : "#2563EB";
        sb.AppendLine(@"  <g transform=""translate(170, 150)"">");
        sb.AppendLine($@"    <rect x=""0"" y=""0"" width=""220"" height=""40"" rx=""20"" fill=""{badgeBg}"" fill-opacity=""0.95""/>");
        sb.AppendLine($@"    <text x=""110"" y=""26"" fill=""#FFFFFF"" font-family=""'Inter', sans-serif"" font-size=""15"" font-weight=""800"" text-anchor=""middle"">{badgeText}</text>");
        sb.AppendLine(@"  </g>");

        // Título y Editorial
        sb.AppendLine(@"  <g transform=""translate(140, 620)"">");
        sb.AppendLine($@"    <text x=""0"" y=""40"" fill=""{textPrimary}"" font-family=""'Inter', sans-serif"" font-size=""40"" font-weight=""900"">{Truncate(title, 38)}</text>");
        sb.AppendLine($@"    <text x=""0"" y=""80"" fill=""{textSecondary}"" font-family=""'Inter', sans-serif"" font-size=""22"" font-weight=""600"">Editorial: {publisher}</text>");
        sb.AppendLine(@"  </g>");

        // Píldoras de PVP y Fecha
        sb.AppendLine(@"  <g transform=""translate(140, 740)"">");
        sb.AppendLine($@"    <rect x=""0"" y=""0"" width=""260"" height=""52"" rx=""16"" fill=""{(isDark ? "#1E293B" : "#F1F5F9")}"" stroke=""{cardBorder}"" stroke-width=""1.5""/>");
        sb.AppendLine($@"    <text x=""24"" y=""33"" fill=""{textPrimary}"" font-family=""'Inter', sans-serif"" font-size=""17"" font-weight=""800"">🗓️ {releaseDateStr}</text>");

        sb.AppendLine($@"    <rect x=""280"" y=""0"" width=""240"" height=""52"" rx=""16"" fill=""{(isDark ? "#1E293B" : "#F1F5F9")}"" stroke=""{cardBorder}"" stroke-width=""1.5""/>");
        sb.AppendLine($@"    <text x=""304"" y=""33"" fill=""#10B981"" font-family=""'Inter', sans-serif"" font-size=""17"" font-weight=""800"">💶 {pvpStr}</text>");
        sb.AppendLine(@"  </g>");

        // Pie
        sb.AppendLine(@"  <g transform=""translate(140, 880)"">");
        sb.AppendLine($@"    <rect x=""0"" y=""0"" width=""800"" height=""100"" rx=""20"" fill=""{(isDark ? "#0F172A" : "#F8FAFC")}"" stroke=""{cardBorder}"" stroke-width=""1""/>");
        sb.AppendLine(@"    <text x=""40"" y=""42"" fill=""#F97316"" font-family=""'Inter', sans-serif"" font-size=""18"" font-weight=""800"">LUDEKA &bull; CATÁLOGO EDITORIAL</text>");
        sb.AppendLine($@"    <text x=""40"" y=""72"" fill=""{textPrimary}"" font-family=""'Inter', sans-serif"" font-size=""20"" font-weight=""700"">Ficha completa y tiendas colaboradoras en ludeka.app</text>");
        sb.AppendLine(@"  </g>");

        sb.AppendLine(@"</svg>");
        return sb.ToString();
    }

    private string ComposeGameSvg(Game game, bool isDark)
    {
        var minPlayers = game.Scalability.Count > 0 ? game.Scalability.Min(s => s.PlayerCount) : 1;
        var maxPlayers = game.Scalability.Count > 0 ? game.Scalability.Max(s => s.PlayerCount) : 4;
        var idealPlayers = minPlayers == maxPlayers ? $"{minPlayers} jugadores" : $"{minPlayers}-{maxPlayers} jugadores";

        var data = new SocialCardDataDto(
            GameTitle: game.SpanishTitle,
            OriginalTitle: game.OriginalTitle,
            Designer: game.Designer,
            Publisher: game.Publisher,
            YearPublished: game.YearPublished,
            Rating: game.LudistRating > 0 ? game.LudistRating : game.BggRating,
            StyleText: game.Style.ToString(),
            ConfrontationText: game.Confrontation.ToString(),
            IdealPlayersText: idealPlayers,
            EstimatedDurationPerPlayer: game.Duration?.EstimatedPerPlayerMinutes,
            CoverImageUrl: game.CoverImageUrl,
            FoundingVerdictBadge: null
        );

        return _socialCardService.GenerateSvg(data);
    }

    private static string ComposeGenericSvg(string title, bool isDark)
    {
        var safeTitle = EscapeXml(title);
        var bgColor = isDark ? "#0B0F17" : "#F8FAFC";
        var textColor = isDark ? "#FFFFFF" : "#0F172A";

        return $@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 1080 1080"" width=""1080"" height=""1080"">
  <rect width=""1080"" height=""1080"" fill=""{bgColor}""/>
  <text x=""540"" y=""500"" fill=""#F97316"" font-family=""'Inter', sans-serif"" font-size=""48"" font-weight=""900"" text-anchor=""middle"">Ludeka.</text>
  <text x=""540"" y=""580"" fill=""{textColor}"" font-family=""'Inter', sans-serif"" font-size=""32"" font-weight=""700"" text-anchor=""middle"">{Truncate(safeTitle, 35)}</text>
  <text x=""540"" y=""630"" fill=""#94A3B8"" font-family=""'Inter', sans-serif"" font-size=""20"" text-anchor=""middle"">Comunidad de Juegos de Mesa en Español &bull; ludeka.app</text>
</svg>";
    }

    private static string GenerateGiveawayCaption(Giveaway g)
    {
        var handle = FormatSocialHandle(g.Organizer);
        var collabText = !string.IsNullOrWhiteSpace(g.Collaborator)
            ? $" y {FormatSocialHandle(g.Collaborator)}"
            : string.Empty;

        var gameText = !string.IsNullOrWhiteSpace(g.GameTitle)
            ? $"\n🎲 Juego: {g.GameTitle}"
            : string.Empty;

        var countryText = g.IsInternational ? "Internacional 🌎" : g.Country;

        return $@"🎁 ¡SORTEO ACTIVO! {g.Title}

📢 Organizado por {handle}{collabText}{gameText}
🌍 Ámbito: {countryText}
⏳ Fecha límite: {g.DeadlineAt:dd/MM/yyyy HH:mm} (hora peninsular española)

🔗 Enlace oficial de participación y todos los detalles en ludeka.app/sorteos (enlace directo en nuestra bio).

¡Mucha suerte a todas las mesas de juego! 🎲✨

---
#sorteojuegos #juegosdemesa #boardgames #ludeka #sorteo #{CleanTag(g.Organizer)} #juegosdemesaespaña #sorteos #comunidadludica";
    }

    private static string GenerateReleaseCaption(WeeklyRelease r)
    {
        var badge = r.IsReprint ? "🔄 ¡REIMPRESIÓN DISPONIBLE!" : "🆕 ¡NOVEDAD EDITORIAL!";
        var handle = FormatSocialHandle(r.Publisher);
        var pvpText = r.EstimatedPvp.HasValue ? $"\n💶 PVP estimado: ~{r.EstimatedPvp.Value:0.00} €" : string.Empty;
        var notesText = !string.IsNullOrWhiteSpace(r.Notes) ? $"\n📝 {r.Notes}" : string.Empty;
        var releaseDateText = r.ReleaseDate.HasValue ? r.ReleaseDate.Value.ToString("dd/MM/yyyy") : "Próximamente";

        return $@"{badge} {r.Title}

🏢 Editorial: {handle}
🗓️ Fecha de lanzamiento: {releaseDateText}{pvpText}{notesText}

👉 Consulta su ficha técnica, semáforo de comensales y disponibilidad en tiendas colaboradoras en ludeka.app/novedades (enlace en bio).

---
#novedadesludicas #juegosdemesa #boardgames #ludeka #{CleanTag(r.Publisher)} #lanzamientos #juegosdemesaespaña #bgg";
    }

    private string GenerateGameCaption(Game game)
    {
        var minPlayers = game.Scalability.Count > 0 ? game.Scalability.Min(s => s.PlayerCount) : 1;
        var maxPlayers = game.Scalability.Count > 0 ? game.Scalability.Max(s => s.PlayerCount) : 4;
        var idealPlayers = minPlayers == maxPlayers ? $"{minPlayers} jugadores" : $"{minPlayers}-{maxPlayers} jugadores";

        var data = new SocialCardDataDto(
            GameTitle: game.SpanishTitle,
            OriginalTitle: game.OriginalTitle,
            Designer: game.Designer,
            Publisher: game.Publisher,
            YearPublished: game.YearPublished,
            Rating: game.LudistRating > 0 ? game.LudistRating : game.BggRating,
            StyleText: game.Style.ToString(),
            ConfrontationText: game.Confrontation.ToString(),
            IdealPlayersText: idealPlayers,
            EstimatedDurationPerPlayer: game.Duration?.EstimatedPerPlayerMinutes,
            CoverImageUrl: game.CoverImageUrl,
            FoundingVerdictBadge: null
        );

        return _socialCardService.GenerateInstagramCaption(data);
    }

    private static string FormatSocialHandle(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var clean = name.Trim();
        if (clean.StartsWith('@'))
            return clean;

        return $"@{clean.Replace(" ", "").ToLowerInvariant()}";
    }

    private static string CleanTag(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return "ludeka";

        return input
            .Replace(" ", "")
            .Replace("-", "")
            .Replace(".", "")
            .ToLowerInvariant();
    }

    private static string Truncate(string input, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(input) || input.Length <= maxLength)
            return input;

        return string.Concat(input.AsSpan(0, maxLength - 1), "…");
    }

    private static string EscapeXml(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        return input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");
    }
}
