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

        // ==== ИНИЦИАЛИЗАЦИЯ СЕРВИСОВ ====
        var storage = new JsonStorageService();
        var state = new UserStateService(storage);

        // ==== МОДУЛИ ====
        var glucose = new GlucoseModule(_bot, state, storage);
        var bu = new BreadUnitsModule(_bot, state, storage);
        var school = new DiabetesSchoolModule(_bot, state, storage);

        // ==== HANDLERS ====
        _callbackHandler = new CallbackHandler(_bot, state, storage, glucose, bu, school);
        _commandHandler = new CommandHandler(_bot, state, storage, glucose, bu, school, _callbackHandler);

        _callbackHandler.SetCommandHandler(_commandHandler);

        Logger.Info("[BOT] BotService создан");
    }

    // ====================================================================
    //  ОБРАБОТКА АПДЕЙТОВ ОТ ВЕБХУКА
    // ====================================================================
    public async Task HandleUpdateAsync(Update update)
    {
        try
        {
            if (update.Message is not null)
            {
                var m = update.Message;
                Logger.Info($"[BOT] Update: Message chatId={m.Chat.Id}, text='{m.Text}'");

                await _commandHandler.HandleMessageAsync(m, CancellationToken.None);

                Logger.Info("[BOT] Message обработано");
                return;
            }

            if (update.CallbackQuery is not null)
            {
                var cb = update.CallbackQuery;
                Logger.Info($"[BOT] Update: CallbackQuery userId={cb.From.Id}, data='{cb.Data}'");

                await _callbackHandler.HandleAsync(cb, CancellationToken.None);

                Logger.Info("[BOT] Callback обработано");
                return;
            }

            Logger.Info("[BOT] Неизвестный тип апдейта → игнор");
        }
        catch (Exception ex)
        {
            Logger.Error("[BOT] Ошибка обработки апдейта", ex);
        }
    }

    // ====================================================================
    //  УСТАНОВКА ВЕБХУКА (НЕОБЯЗАТЕЛЬНО, НО МОЖНО ВЫЗВАТЬ ИЗВНЕ)
    // ====================================================================
    public async Task SetWebhookAsync(string url)
    {
        try
        {
            Logger.Info($"[BOT] Устанавливаю webhook: {url}");
            await _bot.SetWebhook(url);
        }
        catch (Exception ex)
        {
            Logger.Error("[BOT] Ошибка при установке webhook", ex);
        }
    }

    public async Task ProcessUpdate(Update update)
{
    try
    {
        // Message
        if (update.Message != null)
        {
            await _commandHandler.HandleMessageAsync(update.Message, default);
            return;
        }

        // Callback
        if (update.CallbackQuery != null)
        {
            await _callbackHandler.HandleAsync(update.CallbackQuery, default);
            return;
        }
    }
    catch (Exception ex)
    {
        Logger.Error("ProcessUpdate error", ex);
    }
}

public async Task SetWebhookAsync(string url)
{
    await _bot.DeleteWebhookAsync();
    await _bot.SetWebhookAsync(url);
    Logger.Info($"Webhook set to: {url}");
}

}

