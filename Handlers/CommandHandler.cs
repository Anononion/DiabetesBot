using Telegram.Bot;
using Telegram.Bot.Types;
using DiabetesBot.Services;
using DiabetesBot.Modules;
using DiabetesBot.Models;
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

        BotLogger.Info("[CMD] CommandHandler initialized");
    }

    // ============================================================
    // MAIN ENTRY FOR MESSAGE
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

        var phase = await _state.GetPhaseAsync(userId);

        // ============================================================
        // 1) IF WAITING FOR LANGUAGE → IGNORE TEXT (WAIT CALLBACK)
        // ============================================================
        if (phase == UserPhase.ChoosingLanguage)
        {
            BotLogger.Info("[CMD] Ignoring text during ChoosingLanguage");
            return;
        }

        // ============================================================
        // 2) /start — language selection
        // ============================================================
        if (text == "/start")
        {
            await StartAsync(chatId, userId, ct);
            return;
        }

        // ============================================================
        // 3) MAIN MENU BUTTONS
        // ============================================================
        if (text == (lang == "kk" ? "📈 Қандағы қант" : "📈 Глюкоза"))
        {
            await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);
            await _glucose.ShowMain(chatId, lang, ct);
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
        // 4) GLUCOSE FLOW
        // ============================================================
        if (phase == UserPhase.GlucoseMenu)
        {
            await _glucose.HandleMessage(chatId, text, lang, ct);
            return;
        }

        if (phase == UserPhase.AwaitGlucoseValue)
        {
            await _glucose.HandleValueInput(msg, ct);
            return;
        }

        // ============================================================
        // 5) BREAD UNITS FLOW
        // ============================================================
        if (phase == UserPhase.BreadUnits)
        {
            await _bu.HandleText(chatId, text, lang, ct);
            return;
        }

        // ============================================================
        // 6) DIABETES SCHOOL FLOW
        // ============================================================
        if (phase == UserPhase.DiabetesSchool)
        {
            await _school.HandleTextAsync(userId, chatId, text, ct);
            return;
        }

        // ============================================================
        // 7) DEFAULT → MAIN MENU
        // ============================================================
        await SendMainMenuAsync(chatId, lang, ct);
    }

    // ============================================================
    // START
    // ============================================================
    private async Task StartAsync(long chatId, long userId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(userId);

        if (user.Language is null or "" || user.Phase == UserPhase.New)
        {
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);

            await _bot.SendMessage(
                chatId,
                "Выберите язык / Тілді таңдаңыз:",
                replyMarkup: KeyboardBuilder.LanguageChoice(),
                cancellationToken: ct
            );
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
    // MAIN MENU
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
