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
    private readonly CallbackHandler _callback;

    public CommandHandler(
        TelegramBotClient bot,
        UserStateService state,
        JsonStorageService storage,
        GlucoseModule glucose,
        BreadUnitsModule bu,
        DiabetesSchoolModule school,
        CallbackHandler callback)
    {
        _bot = bot;
        _state = state;
        _storage = storage;
        _glucose = glucose;
        _bu = bu;
        _school = school;
        _callback = callback;
    }

    // ---------------------------------------------------------
    // Главное меню
    // ---------------------------------------------------------
    public async Task SendMainMenuAsync(long chatId, string lang, CancellationToken ct)
    {
        var kb = KeyboardBuilder.MainMenu(lang);

        string txt = lang == "kk"
            ? "Бөлімді таңдаңыз:"
            : "Выберите раздел:";

        await _bot.SendMessage(chatId, txt, replyMarkup: kb, cancellationToken: ct);
        await _state.SetPhaseAsync(chatId, UserPhase.MainMenu);
    }

    // ---------------------------------------------------------
    // Settings (Настройки)
    // ---------------------------------------------------------
    private async Task ShowSettingsMenu(long chatId, string lang, CancellationToken ct)
    {
        string btnLang = lang == "kk" ? "🌐 Тілді ауыстыру" : "🌐 Сменить язык";
        string btnAuthors = lang == "kk" ? "👤 Авторлар" : "👤 Авторы";

        var kb = KeyboardBuilder.Menu(new[] { btnLang, btnAuthors }, lang, true);

        string msg = lang == "kk" ? "Параметрлер:" : "Настройки:";
        await _bot.SendMessage(chatId, msg, replyMarkup: kb, cancellationToken: ct);
    }

    private async Task ShowAuthorsAsync(long chatId, string lang, CancellationToken ct)
    {
        string dev = lang == "kk"
            ? "👨‍💻 @Batyr_dot_bat — әзірлеуші"
            : "👨‍💻 @Batyr_dot_bat — разработчик";

        string medic = lang == "kk"
            ? "👩‍⚕️ @Adiya_ua — медициналық сарапшы"
            : "👩‍⚕️ @Adiya_ua — медицинский эксперт";

        await _bot.SendMessage(chatId, dev, cancellationToken: ct);
        await _bot.SendMessage(chatId, medic, cancellationToken: ct);

        var kb = KeyboardBuilder.BackToMenu(lang);
        string back = lang == "kk" ? "Артқа" : "Назад";

        await _bot.SendMessage(chatId, back, replyMarkup: kb, cancellationToken: ct);
    }

    // ---------------------------------------------------------
    // ТЕКСТ
    // ---------------------------------------------------------
    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        if (msg.Text is null) return;

        long chatId = msg.Chat.Id;
        long userId = msg.From!.Id;
        string text = msg.Text;

        var user = await _storage.LoadAsync(userId);
        string lang = string.IsNullOrWhiteSpace(user.Language) ? "ru" : user.Language;

        // ---- /start ----
        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);

            await _bot.SendMessage(chatId,
                "Выберите язык / Тілді таңдаңыз:",
                replyMarkup: KeyboardBuilder.LanguageChoice(),
                cancellationToken: ct);

            return;
        }

        // На этапе выбора языка игнорируем текст
        if (await _state.GetPhaseAsync(userId) == UserPhase.ChoosingLanguage)
        {
            await _bot.SendMessage(chatId,
                lang == "kk"
                    ? "Тілді төмендегі батырмадан таңдаңыз."
                    : "Используйте кнопки ниже для выбора языка.",
                cancellationToken: ct);
            return;
        }

        // =====================================================
        // ГЛАВНЫЕ КНОПКИ
        // =====================================================
        if (text == KeyboardBuilder.Button_Glucose(lang))
        {
            await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);
            await _glucose.ShowMain(chatId, lang, ct);
            return;
        }

        if (text == KeyboardBuilder.Button_BreadUnits(lang))
        {
            await _state.SetPhaseAsync(userId, UserPhase.BreadUnits);
            await _bu.ShowMain(chatId, lang, ct);
            return;
        }

        if (text == KeyboardBuilder.Button_School(lang))
        {
            await _state.SetPhaseAsync(userId, UserPhase.DiabetesSchool);
            await _school.ShowMainMenuAsync(chatId, userId, ct);
            return;
        }

        if (text == KeyboardBuilder.Button_Settings(lang))
        {
            await _state.SetPhaseAsync(userId, UserPhase.Settings);
            await ShowSettingsMenu(chatId, lang, ct);
            return;
        }

        if (text == KeyboardBuilder.Button_Back(lang))
        {
            await _state.SetPhaseAsync(userId, UserPhase.MainMenu);
            await SendMainMenuAsync(chatId, lang, ct);
            return;
        }

        // =====================================================
        // SETTINGS SUBMENU
        // =====================================================
        if (text == (lang == "kk" ? "🌐 Тілді ауыстыру" : "🌐 Сменить язык"))
        {
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);
            await _bot.SendMessage(chatId,
                "Выберите язык / Тілді таңдаңыз:",
                replyMarkup: KeyboardBuilder.LanguageChoice(),
                cancellationToken: ct);
            return;
        }

        if (text == (lang == "kk" ? "👤 Авторлар" : "👤 Авторы"))
        {
            await ShowAuthorsAsync(chatId, lang, ct);
            return;
        }

        // =====================================================
        // PHASE ROUTING
        // =====================================================

        // Glucose
        if (await _state.GetPhaseAsync(userId) == UserPhase.AwaitGlucoseValue)
        {
            await _glucose.HandleValueInput(msg, ct);
            return;
        }

        // BU
        if (_state.GetState(userId).State.Step == UserStep.BU_WaitWeight)
        {
            await _bu.HandleText(chatId, text, lang, ct);
            return;
        }

        // School
        if (await _state.GetPhaseAsync(userId) == UserPhase.DiabetesSchool)
        {
            await _school.HandleTextAsync(userId, chatId, text, ct);
            return;
        }

        // Fallback
        await _bot.SendMessage(chatId,
            lang == "kk"
                ? "Түсініксіз команда. Мәзірді пайдаланыңыз."
                : "Неизвестная команда. Используйте меню.",
            cancellationToken: ct);
    }

    // ---------------------------------------------------------
    // CALLBACK ROUTER
    // ---------------------------------------------------------
    public async Task HandleCallbackAsync(CallbackQuery query, CancellationToken ct)
    {
        await _callback.HandleAsync(query, ct);
    }
}
