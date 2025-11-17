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
    //  ЗАПУСК БОТА
    // ====================================================================
    public async Task StartAsync()
    {
        Logger.Info("[BOT] Запуск StartAsync");

        _bot.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync
        );

        Logger.Info("Bot started. Waiting for updates...");

        await Task.Delay(-1);
    }

    // ====================================================================
    //  ОБРАБОТКА АПДЕЙТОВ
    // ====================================================================
    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
    {
        try
        {
            // --- Сообщение ---
            if (update.Message is not null)
            {
                var m = update.Message;
                Logger.Info($"[BOT] Update: Message от chatId={m.Chat.Id}, text='{m.Text}'");

                await _commandHandler.HandleMessageAsync(m, ct);

                Logger.Info("[BOT] Update: Message обработано CommandHandler");
                return;
            }

            // --- Callback ---
            if (update.CallbackQuery is not null)
            {
                var cb = update.CallbackQuery;
                Logger.Info($"[BOT] Update: CallbackQuery от userId={cb.From.Id}, data='{cb.Data}'");

                await _callbackHandler.HandleAsync(cb, ct);

                Logger.Info("[BOT] Update: CallbackQuery обработано CallbackHandler");
                return;
            }

            // --- Неизвестный апдейт ---
            Logger.Info("[BOT] Update: неизвестный тип апдейта, игнорируем");
        }
        catch (Exception ex)
        {
            Logger.Error("[BOT] Ошибка в HandleUpdateAsync", ex);
        }
    }

    // ====================================================================
    //  ОШИБКИ TELEGRAM BOT API
    // ====================================================================
    private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
    {
        Logger.Error("[BOT] Telegram polling error", ex);
        return Task.CompletedTask;
    }
}
