using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Text.Json;
using System.Text.Json.Serialization;
using DiabetesBot.Handlers;
using DiabetesBot.Modules;
using DiabetesBot.Utils;
using DiabetesBot.Services;

namespace DiabetesBot;

public class BotService
{
    private readonly ITelegramBotClient _bot;

    private readonly CommandHandler _cmd;
    private readonly CallbackHandler _cb;

    private readonly GlucoseModule _glucose;
    private readonly BreadUnitsModule _bread;
    private readonly DiabetesSchoolModule _school;

    public BotService(string token)
    {
        BotLogger.Info("[BOT] Initializing BotService…");

        _bot = new TelegramBotClient(token);

        // === Создаём модули ===
        _glucose = new GlucoseModule(_bot);
        _bread = new BreadUnitsModule(_bot);
        _school = new DiabetesSchoolModule(_bot);

        // === Создаём хэндлеры ===
        _cmd = new CommandHandler(_bot, _glucose, _bread, _school);
        _cb = new CallbackHandler(_bot, _cmd, _glucose, _bread, _school);

        BotLogger.Info("[BOT] BotService initialized successfully");
    }

    // ============================================================
    // MAIN UPDATE ENTRY
    // ============================================================

    public async Task HandleWebhookAsync(Update update)
    {
        try
        {
            BotLogger.Info($"[BOT] Update received: type={update.Type}");
            BotLogger.Info("[DEBUG] RAW UPDATE JSON: " +
            JsonSerializer.Serialize(
                update,
                new JsonSerializerOptions {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.Never
            }    
        )
    );

            // 1. CALLBACKS — по наличию, а НЕ по type
            if (update.CallbackQuery != null)
            {
                BotLogger.Info("[BOT] Update received: type=CallbackQuery");
                BotLogger.Info("[DEBUG] RAW CALLBACK: " + JsonSerializer.Serialize(update));
                await _cb.HandleCallbackAsync(update.CallbackQuery, CancellationToken.None);
                return;
            }

            // 2. Сообщения
            if (update.Message != null)
            {
                await _cmd.HandleMessageAsync(update.Message, CancellationToken.None);
                return;
            }

            // 3. Логи если неизвестно что пришло
            BotLogger.Warn("[BOT] Unknown update type → ignore");
        }
        catch (Exception ex)
        {
            BotLogger.Error("[BOT] ERROR during update handling", ex);
        }
    }



    // ============================================================
    // WEBHOOK MANAGEMENT
    // ============================================================

    public async Task SetWebhookAsync(string url)
    {
        BotLogger.Info($"[BOT] Setting webhook to: {url}");

        await _bot.DeleteWebhook(dropPendingUpdates: true);
        await _bot.SetWebhook(url);

        BotLogger.Info("[BOT] Webhook installed successfully");
    }
}









