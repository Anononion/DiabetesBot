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

        var storage = new JsonStorageService();
        var state = new UserStateService(storage);

        var glucose = new GlucoseModule(_bot, state, storage);
        var bu = new BreadUnitsModule(_bot, state, storage);
        var school = new DiabetesSchoolModule(_bot, state, storage);

        _callbackHandler = new CallbackHandler(_bot, state, storage, glucose, bu, school);
        _commandHandler = new CommandHandler(_bot, state, storage, glucose, bu, school);

        _callbackHandler.SetCommandHandler(_commandHandler);

        BotLogger.Info("[BOT] BotService инициализирован");
    }

    public async Task HandleWebhookAsync(Update update)
    {
        try
        {
            // === CALLBACK ===
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                await _callbackHandler.HandleAsync(update.CallbackQuery, CancellationToken.None);
                return;
            }

            // === MESSAGE ===
            if (update.Type == UpdateType.Message && update.Message != null)
            {
                await _commandHandler.HandleMessageAsync(update.Message, CancellationToken.None);
                return;
            }

            // === СТРАХОВОЧНЫЙ ОБРАБОТЧИК CALLBACK, который Telegram присылает иначе ===
            if (update is { CallbackQuery: { } cb2 })
            {
                await _callbackHandler.HandleAsync(cb2, CancellationToken.None);
                return;
            }

            BotLogger.Info("[BOT] Неизвестный тип обновления → игнор");
        }
        catch (Exception ex)
        {
            BotLogger.Error("[BOT] Ошибка обработки апдейта", ex);
        }
    }

    public async Task SetWebhookAsync(string url)
    {
        await _bot.DeleteWebhookAsync(dropPendingUpdates: true);
        await _bot.SetWebhookAsync(url);
        BotLogger.Info($"Webhook установлен: {url}");
    }
}
