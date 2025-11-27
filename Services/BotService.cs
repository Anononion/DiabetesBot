using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using DiabetesBot.Handlers;
using DiabetesBot.Services;
using DiabetesBot.Modules;
using DiabetesBot.Utils;

namespace DiabetesBot.Services;

public class BotService
{
    private readonly string _token;
    private readonly TelegramBotClient _bot;

    private readonly CallbackHandler _callbackHandler;
    private readonly CommandHandler _commandHandler;

    public BotService(string token)
    {
        _token = token;
        _bot = new TelegramBotClient(token);

        // === Инициализация сервисов ===
        var storage = new JsonStorageService();
        var state = new UserStateService();

        // === Модули ===
        var glucose = new GlucoseModule(_bot, state, storage);
        var bu = new BreadUnitsModule(_bot, state, storage);
        var school = new DiabetesSchoolModule(_bot, state, storage);

        // === Handlers ===
        _callbackHandler = new CallbackHandler(_bot, state, storage, glucose, bu, school);

        // CommandHandler принимает 6 аргументов (без callbackHandler!)
        _commandHandler = new CommandHandler(
            _bot,
            state,
            storage,
            glucose,
            bu,
            school
        );

        // Связываем callback → command
        _callbackHandler.SetCommandHandler(_commandHandler);
    }

    // ============================================================
    // START BOT
    // ============================================================
    public async Task StartAsync()
    {
        BotLogger.Info("[BOT] Запуск...");

        try
        {
            // Сброс webhook
            await _bot.DeleteWebhookAsync(dropPendingUpdates: true);
            BotLogger.Info("[BOT] Старый webhook удалён.");

            string url = $"https://diacare-2x9i.onrender.com/webhook/{_token}";

            await _bot.SetWebhookAsync(
                url,
                allowedUpdates: Array.Empty<UpdateType>()
            );

            BotLogger.Info($"[BOT] Webhook установлен: {url}");
        }
        catch (Exception ex)
        {
            BotLogger.Error($"[BOT] Ошибка установки вебхука: {ex.Message}");
        }
    }

    // ============================================================
    // PROCESS UPDATE
    // ============================================================
    public async Task ProcessUpdateAsync(Telegram.Bot.Types.Update update)
    {
        try
        {
            if (update.Type == UpdateType.CallbackQuery)
            {
                await _callbackHandler.HandleCallbackAsync(update.CallbackQuery!);
                return;
            }

            if (update.Type == UpdateType.Message)
            {
                if (update.Message!.Text != null)
                {
                    await _commandHandler.HandleMessageAsync(update.Message, CancellationToken.None);
                    return;
                }
            }

            BotLogger.Info("[BOT] Неизвестный тип обновления → игнор");
        }
        catch (Exception ex)
        {
            BotLogger.Error($"[BOT] Ошибка обработки обновления: {ex}");
        }
    }
}
