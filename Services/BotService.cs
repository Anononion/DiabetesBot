using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using DiabetesBot.Handlers;
using DiabetesBot.Utils;

namespace DiabetesBot;

public class BotService
{
    private readonly TelegramBotClient _bot;
    private readonly CommandHandler _commandHandler;

    public BotService(string token)
    {
        BotLogger.Info("[BOT] Initializing BotService…");

        _bot = new TelegramBotClient(token);

        // Наш новый, чистый хэндлер
        _commandHandler = new CommandHandler(_bot);

        BotLogger.Info("[BOT] BotService initialized");
    }

    // ============================================================
    // HANDLE UPDATE
    // ============================================================

    public async Task HandleWebhookAsync(Update update)
    {
        BotLogger.Info($"[BOT] UPDATE received: type={update.Type}");

        try
        {
            if (update.Message != null)
            {
                BotLogger.Info(
                    $"[BOT] Message: text='{update.Message.Text}', chat={update.Message.Chat.Id}, user={update.Message.From?.Id}"
                );

                await _commandHandler.HandleMessageAsync(
                    update.Message,
                    CancellationToken.None
                );

                BotLogger.Info("[BOT] Message processed");
                return;
            }

            if (update.CallbackQuery != null)
            {
                BotLogger.Info(
                    $"[BOT] Callback: data='{update.CallbackQuery.Data}', from={update.CallbackQuery.From.Id}"
                );

                // позже сделаем новый CallbackHandler, если нужно
                BotLogger.Warn("[BOT] No callback handler implemented yet");
                return;
            }

            BotLogger.Warn("[BOT] Unknown update — ignored");
        }
        catch (Exception ex)
        {
            BotLogger.Error("[BOT] Error while handling update", ex);
        }
    }

    // ============================================================
    // SET WEBHOOK
    // ============================================================

    public async Task SetWebhookAsync(string url)
    {
        BotLogger.Info($"[BOT] Setting webhook: {url}");

        await _bot.DeleteWebhookAsync(dropPendingUpdates: true);
        await _bot.SetWebhookAsync(url);

        BotLogger.Info("[BOT] Webhook installed");
    }
}
