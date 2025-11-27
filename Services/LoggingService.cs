using DiabetesBot.Utils;
using Telegram.Bot;

namespace DiabetesBot.Services;

public class LoggingService
{
    private readonly TelegramBotClient? _bot;
    private readonly long? _adminId;
    private readonly bool _toTelegram;

    public LoggingService(TelegramBotClient? bot = null)
    {
        _bot = bot;
        _toTelegram = (Environment.GetEnvironmentVariable("LOG_TO_TELEGRAM") ?? "false")
            .Equals("true", StringComparison.OrdinalIgnoreCase);

        if (long.TryParse(Environment.GetEnvironmentVariable("ADMIN_ID"), out var id))
            _adminId = id;
    }

    public async Task NotifyCriticalAsync(string text, CancellationToken ct = default)
    {
        if (!_toTelegram || _bot is null || _adminId is null) return;

        try
        {
            await _bot.SendMessage(
                chatId: _adminId.Value,
                text: "🔥 *CRITICAL*\n" + text,
                parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            BotLogger.Error("Failed to send admin notification", ex);
        }
    }
}
