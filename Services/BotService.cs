using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

using DiabetesBot.Handlers;
using DiabetesBot.Services;
using DiabetesBot.Modules;
using DiabetesBot.Utils;

namespace DiabetesBot;

public class BotService
{
    private readonly TelegramBotClient _bot;
    private readonly CommandHandler _commandHandler;
    private readonly CallbackHandler _callbackHandler;

    public BotService(string token)
    {
        _bot = new TelegramBotClient(token);

        // === Сервисы ===
        var storage = new JsonStorageService();
        var state = new UserStateService(storage);

        // === Модули ===
        var glucose = new GlucoseModule(_bot, state, storage);
        var bu = new BreadUnitsModule(_bot, state, storage);
        var school = new DiabetesSchoolModule(_bot, state, storage);

        // === Handlers ===
        _callbackHandler = new CallbackHandler(_bot, state, storage, glucose, bu, school);

        // command handler принимает РОВНО 6 аргументов
        _commandHandler = new CommandHandler(
            _bot,
            state,
            storage,
            glucose,
            bu,
            school
        );

        _callbackHandler.SetCommandHandler(_commandHandler);

        BotLogger.Info("[BOT] BotService создан");
    }

    // ============================================================
    // HANDLE WEBHOOK UPDATE
    // ============================================================
    public async Task HandleWebhookAsync(Update update)
    {
        try
        {
            if (update.CallbackQuery is not null)
            {
                await _callbackHandler.HandleAsync(update.CallbackQuery, CancellationToken.None);
                return;
            }

            if (update.Message is not null)
            {
                await _commandHandler.HandleMessageAsync(update.Message, CancellationToken.None);
                return;
            }

            BotLogger.Info("[BOT] Неизвестный тип обновления → игнор");
        }
        catch (Exception ex)
        {
            BotLogger.Error("[BOT] Ошибка обработки апдейта", ex);
        }
    }

    // ============================================================
    // SET WEBHOOK
    // ============================================================
    public async Task SetWebhookAsync(string url)
    {
        await _bot.DeleteWebhookAsync(dropPendingUpdates: true);
        await _bot.SetWebhookAsync(url);
        BotLogger.Info($"Webhook set to: {url}");
    }
}
