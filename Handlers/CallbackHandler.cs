using Telegram.Bot;
using Telegram.Bot.Types;
using DiabetesBot.Services;
using DiabetesBot.Modules;
using DiabetesBot.Models;
using DiabetesBot.Utils;

namespace DiabetesBot.Handlers;

public class CallbackHandler
{
    private readonly TelegramBotClient _bot;
    private readonly UserStateService _state;
    private readonly JsonStorageService _storage;

    private readonly GlucoseModule _glucose;
    private readonly BreadUnitsModule _bu;
    private readonly DiabetesSchoolModule _school;

    private CommandHandler? _commandHandler;

    public CallbackHandler(
        TelegramBotClient bot,
        UserStateService state,
        JsonStorageService storage,
        GlucoseModule glucose,
        BreadUnitsModule bu,
        DiabetesSchoolModule school)
    {
        _bot = bot;
        _state = state;
        _storage = storage;
        _glucose = glucose;
        _bu = bu;
        _school = school;

        Logger.Info("[CB] CallbackHandler создан");
    }

    public void SetCommandHandler(CommandHandler handler)
    {
        _commandHandler = handler;
        Logger.Info("[CB] CommandHandler привязан к CallbackHandler");
    }

    public async Task HandleAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Data is null)
        {
            Logger.Warn("[CB] query.Data == null → игнор");
            return;
        }

        string data = query.Data;
        long chatId = query.Message!.Chat.Id;
        long userId = query.From.Id;

        Logger.Info($"[CB] Callback: userId={userId}, chatId={chatId}, data='{data}'");

        // ------------------------------------------------------------------
        // 1) ВЫБОР ЯЗЫКА
        // ------------------------------------------------------------------
        if (data == "lang_ru" || data == "lang_kk")
        {
            var user = await _storage.LoadAsync(userId);
            user.Language = data == "lang_ru" ? "ru" : "kk";
            await _storage.SaveAsync(user);

            await _state.SetPhaseAsync(userId, UserPhase.MainMenu);

            string msg = user.Language == "ru"
                ? "Язык изменён 🇷🇺"
                : "Тіл өзгертілді 🇰🇿";

            await _bot.SendMessage(chatId, msg, cancellationToken: ct);

            // показать меню после смены языка
            if (_commandHandler != null)
                await _commandHandler.SendMainMenuAsync(chatId, user.Language, ct);

            return;
        }

        // ------------------------------------------------------------------
        // 2) ГЛЮКОМЕТРИЯ — type выбора
        // ------------------------------------------------------------------
        if (data.StartsWith("measure_"))
        {
            await _glucose.HandleCallbackAsync(query, ct);
            return;
        }

        // ------------------------------------------------------------------
        // 3) ХЛЕБНЫЕ ЕДИНИЦЫ
        // ------------------------------------------------------------------
        if (data.StartsWith("BU_"))
        {
            await _bu.HandleButton(chatId, data, ct);
            return;
        }

        // ------------------------------------------------------------------
        // 4) ШКОЛА ДИАБЕТА
        // ------------------------------------------------------------------
        if (data.StartsWith("DS_"))
        {
            await _school.HandleCallbackAsync(query, ct);
            return;
        }

        // ------------------------------------------------------------------
        // НЕИЗВЕСТНЫЙ CALLBACK
        // ------------------------------------------------------------------
        Logger.Warn($"[CB] Неизвестный callback: {data}");
    }
}
