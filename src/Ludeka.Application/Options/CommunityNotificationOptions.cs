namespace Ludeka.Application.Options;

public class CommunityNotificationOptions
{
    public const string SectionName = "CommunityNotifications";

    public bool Enabled { get; set; } = true;
    public bool DryRun { get; set; } = true;

    // Discord
    public string? DiscordWebhookUrl { get; set; }
    public bool DiscordEnabled { get; set; } = true;
    public string DiscordInviteUrl { get; set; } = "https://discord.gg/DgGUUEU6gs";

    // Telegram
    public string? TelegramBotToken { get; set; }
    public string? TelegramChatId { get; set; }
    public bool TelegramEnabled { get; set; } = true;
    public string TelegramChannelUrl { get; set; } = "https://t.me/ludeka";

    // Ko-fi / Mecenazgo
    public string KofiUrl { get; set; } = "https://ko-fi.com/ludeka";

    public bool IsDiscordConfigured => !string.IsNullOrWhiteSpace(DiscordWebhookUrl);
    public bool IsTelegramConfigured => !string.IsNullOrWhiteSpace(TelegramBotToken) && !string.IsNullOrWhiteSpace(TelegramChatId);
}
