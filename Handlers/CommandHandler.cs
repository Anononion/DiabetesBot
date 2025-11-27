using Telegram.Bot;
using Telegram.Bot.Types;
using DiabetesBot.Services;
using DiabetesBot.Modules;
using DiabetesBot.Utils;

namespace DiabetesBot.Handlers;

public class CommandHandler
{
    private readonly TelegramBotClient _bot;
    private readonly UserStateService _state;
    private readonly JsonStorageService _storage;

    private readonly GlucoseModule _glucose;
    private readonly BreadUnitsModule _bu;
    private readonly DiabetesSchoolModule _school;
    private readonly CallbackHandler _callbackHandler;

    public CommandHandler(
        TelegramBotClient bot,
        UserStateService state,
        JsonStorageService storage,
        GlucoseModule glucose,
        BreadUnitsModule bu,
        DiabetesSchoolModule school,
        CallbackHandler callbackHandler)
    {
        _bot = bot;
        _state = state;
        _storage = storage;
        _glucose = glucose;
        _bu = bu;
        _school = school;
        _callbackHandler = callbackHandler;

        BotLogger.Info("[CMD] CommandHandler создан");
    }

    // ============================================================
    // ОСНОВНОЙ ВХОД ДЛЯ MESSAGE
    // ============================================================
    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        if (msg.Text is null)
            return;

        string text = msg.Text;
        long chatId = msg.Chat.Id;
        long userId = msg.From!.Id;

        var user = await _storage.LoadAsync(userId);
        string lang = user.Language ?? "ru";

        // ============================================================
        // 1) Если ждём ВЫБОРА ЯЗЫКА → игнорируем сообщения
        // ============================================================
        var phase = await _state.GetPhaseAsync(userId);
        if (phase == UserPhase.ChoosingLanguage)
        {
            // Ничего не отвечаем — ждём callback-кнопку
            BotLogger.Info("[CMD] ChoosingLanguage: игнорируем текст");
            return;
        }

        // ============================================================
        // 2) Команды
        // ============================================================
        if (text == "/start")
        {
            await StartAsync(chatId, userId, ct);
            return;
        }

        // ============================================================
        // 3) Главные кнопки меню
        // ============================================================

        if (text == (lang == "kk" ? "📈 Қандағы қант" : "📈 Глюкоза"))
        {
            await _state.SetPhaseAsync(userId, UserPhase.Glucose);
            await _glucose.ShowMainMenu(chatId, lang, ct);
            return;
        }

        if (text == (lang == "kk" ? "🥖 ХЕ есептеу" : "🥖 Хлебные единицы"))
        {
            await _state.SetPhaseAsync(userId, UserPhase.BreadUnits);
            await _bu.ShowMain(chatId, lang, ct);
            return;
        }

        if (text == (lang == "kk" ? "📚 Диабет мектебі" : "📚 Школа диабета"))
        {
            await _state.SetPhaseAsync(userId, UserPhase.DiabetesSchool);
            await _school.ShowMainMenuAsync(chatId, userId, ct);
            return;
        }

        if (text == (lang == "kk" ? "⚙️ Параметрлер" : "⚙️ Настройки"))
        {
            await ShowSettings(chatId, lang, ct);
            return;
        }

        // ============================================================
        // 4) Поддержка БУ (ожидание веса)
        // ============================================================
        if (phase == UserPhase.BreadUnits)
        {
            await _bu.HandleText(chatId, text, lang, ct);
            return;
        }

        // ============================================================
        // 5) Поддержка уроков
        // ============================================================
        if (phase == UserPhase.DiabetesSchool)
        {
            await _school.HandleTextAsync(userId, chatId, text, ct);
            return;
        }

        // ============================================================
        // 6) Если непонятно — возвращаем главное меню
        // ============================================================
        await SendMainMenuAsync(chatId, lang, ct);
    }

    // ============================================================
    // СТАРТ
    // ============================================================
    private async Task StartAsync(long chatId, long userId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(userId);

        if (string.IsNullOrWhiteSpace(user.Language))
        {
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);

            await _bot.SendMessage(chatId,
                "Выберите язык / Тілді таңдаңыз:",
                replyMarkup: KeyboardBuilder.LanguageChoice(),
                cancellationToken: ct);

            return;
        }

        await SendMainMenuAsync(chatId, user.Language, ct);
    }

    // ============================================================
    // SETTINGS
    // ============================================================
    private async Task ShowSettings(long chatId, string lang, CancellationToken ct)
    {
        await _bot.SendMessage(
            chatId,
            lang == "kk" ? "⚙️ Тілді таңдаңыз:" : "⚙️ Выберите язык:",
            replyMarkup: KeyboardBuilder.LanguageChoice(),
            cancellationToken: ct
        );
    }

    // ============================================================
    // ГЛАВНОЕ МЕНЮ
    // ============================================================
    public async Task SendMainMenuAsync(long chatId, string lang, CancellationToken ct)
    {
        await _state.SetPhaseAsync(chatId, UserPhase.MainMenu);

        await _bot.SendMessage(
            chatId,
            lang == "kk" ? "Басты мәзір:" : "Главное меню:",
            replyMarkup: KeyboardBuilder.MainMenu(lang),
            cancellationToken: ct
        );
    }
}
