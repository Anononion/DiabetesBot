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
        BotLogger.Info("[BOT] Старт инициализации BotService…");

        _bot = new TelegramBotClient(token);

        var storage = new JsonStorageService();
        var state = new UserStateService(storage);

        var glucose = new GlucoseModule(_bot, state, storage);
        var bu = new BreadUnitsModule(_bot, state, storage);
        var school = new DiabetesSchoolModule(_bot, state, storage);

        _callbackHandler = new CallbackHandler(_bot, state, storage, glucose, bu, school);
        _commandHandler = new CommandHandler(_bot, state, storage, glucose, bu, school);

        _callbackHandler.SetCommandHandler(_commandHandler);

        BotLogger.Info("[BOT] BotService инициализирован полностью");
    }

    public async Task HandleWebhookAsync(Update update)
    {
        BotLogger.Info($"[BOT] Получен UPDATE: type={update.Type}");

        try
        {
            // Логируем все возможные поля
            if (update.Message != null)
            {
                BotLogger.Info($"[BOT] Update.Message: text='{update.Message.Text}', chat={update.Message.Chat.Id}, user={update.Message.From?.Id}");
            }

            if (update.CallbackQuery != null)
            {
                BotLogger.Info($"[BOT] Update.Callback: data='{update.CallbackQuery.Data}', from={update.CallbackQuery.From.Id}");
            }

            // === CALLBACK ===
            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                BotLogger.Info("[BOT] Обработка CallbackQuery → передаём в CallbackHandler");
                await _callbackHandler.HandleAsync(update.CallbackQuery, CancellationToken.None);
                BotLogger.Info("[BOT] Callback обработан успешно");
                return;
            }

            // === MESSAGE ===
            if (update.Type == UpdateType.Message && update.Message != null)
            {
                BotLogger.Info("[BOT] Обработка Message → передаём в CommandHandler");
                await _commandHandler.HandleMessageAsync(update.Message, CancellationToken.None);
                BotLogger.Info("[BOT] Message обработан успешно");
                return;
            }

            // === ПОДСТРАХОВКА (Telegram иногда шлёт Callback по другому) ===
            if (update.CallbackQuery != null)
            {
                BotLogger.Warn("[BOT] Callback пришёл НЕ через update.Type — срабатывает fallback");
                await _callbackHandler.HandleAsync(update.CallbackQuery, CancellationToken.None);
                return;
            }

            BotLogger.Warn("[BOT] Неизвестный тип апдейта — игнор");
        }
        catch (Exception ex)
        {
            BotLogger.Error("[BOT] Ошибка обработки UPDATE", ex);
        }
    }

    public async Task SetWebhookAsync(string url)
    {
        BotLogger.Info($"[BOT] Установка webhook: {url}");

        await _bot.DeleteWebhookAsync(dropPendingUpdates: true);
        await _bot.SetWebhookAsync(url);

        BotLogger.Info($"[BOT] Webhook установлен!");
    }
}
