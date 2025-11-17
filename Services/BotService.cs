using Telegram.Bot;
using Telegram.Bot.Types;
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

        // Сервисы
        var storage = new JsonStorageService();
        var state = new UserStateService(storage);

        // Модули
        var glucose = new GlucoseModule(_bot, state, storage);
        var bu = new BreadUnitsModule(_bot, state, storage);
        var school = new DiabetesSchoolModule(_bot, state, storage);

        // Handlers
        _callbackHandler = new CallbackHandler(_bot, state, storage, glucose, bu, school);
        _commandHandler = new CommandHandler(_bot, state, storage, glucose, bu, school, _callbackHandler);

        _callbackHandler.SetCommandHandler(_commandHandler);

        Logger.Info("[BOT] BotService создан");
    }

    // ================================================================
    //         Обработка обновлений от ВЕБ-ХУКА (единственный метод)
    // ================================================================
    public async Task HandleWebhookAsync(Update update)
    {
        try
        {
            if (update.Message is not null)
            {
                await _commandHandler.HandleMessageAsync(
                    update.Message,
                    CancellationToken.None
                );
                return;
            }

            if (update.CallbackQuery is not null)
            {
                await _callbackHandler.HandleAsync(
                    update.CallbackQuery,
                    CancellationToken.None
                );
                return;
            }

            Logger.Info("[BOT] Неизвестный апдейт → игнор");
        }
        catch (Exception ex)
        {
            Logger.Error("[BOT] Ошибка обработки апдейта", ex);
        }
    }

    // ================================================================
    //                     Установка вебхука
    // ================================================================
    public async Task SetWebhookAsync(string url)
    {
        await _bot.DeleteWebhookAsync();
        await _bot.SetWebhookAsync(url);
        Logger.Info($"Webhook set to: {url}");
    }
}
