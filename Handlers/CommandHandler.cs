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
    private CallbackHandler _callback;

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

    // ============================================================
    // Показ главного меню
    // ============================================================
    public async Task SendMainMenuAsync(long chatId, string lang, CancellationToken ct)
    {
        string btn1 = lang == "kk" ? "📈 Қант өлшеу" : "📈 Глюкометрия";
        string btn2 = lang == "kk" ? "🍞 НБ (нан бірлігі)" : "🍞 Хлебные единицы";
        string btn3 = lang == "kk" ? "📚 Диабет мектебі" : "📚 Школа диабета";
        string btn4 = lang == "kk" ? "⚙️ Баптаулар" : "⚙️ Настройки";

        var kb = KeyboardBuilder.Menu(new[]
        {
            btn1, btn2, btn3, btn4
        });

        string txt = lang == "kk" ? "Басты мәзір:" : "Главное меню:";
        await _bot.SendMessage(chatId, txt, replyMarkup: kb, cancellationToken: ct);
    }

    // ============================================================
    // Основная обработка текста
    // ============================================================
    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        if (msg.Text is null)
            return;

        long chatId = msg.Chat.Id;
        long userId = msg.From!.Id;
        string text = msg.Text;

        var user = await _storage.LoadAsync(userId);
        string lang = user.Language ?? "ru";

        // --------------------------------------------------------
        // /start
        // --------------------------------------------------------
        if (text == "/start")
        {
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);

            await _bot.SendMessage(chatId,
                "Выберите язык / Тілді таңдаңыз:",
                replyMarkup: KeyboardBuilder.LanguageChoice(),
                cancellationToken: ct);

            return;
        }

        // --------------------------------------------------------
        // если фаза — ожидание языка
        // --------------------------------------------------------
        var phase = await _state.GetPhaseAsync(userId);

        if (phase == UserPhase.ChoosingLanguage)
        {
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Тілді батырмалар арқылы таңдаңыз." : "Используйте кнопки ниже.",
                cancellationToken: ct);
            return;
        }

        // --------------------------------------------------------
        // ГЛЮКОМЕТРИЯ: ввод значения
        // --------------------------------------------------------
        if (phase == UserPhase.AwaitGlucoseValue)
        {
            await _glucose.HandleValueInput(msg, ct);
            return;
        }

        // --------------------------------------------------------
        // ГЛАВНОЕ МЕНЮ
        // --------------------------------------------------------
        string btnGlu = lang == "kk" ? "📈 Қант өлшеу" : "📈 Глюкометрия";
        string btnBu = lang == "kk" ? "🍞 НБ (нан бірлігі)" : "🍞 Хлебные единицы";
        string btnSchool = lang == "kk" ? "📚 Диабет мектебі" : "📚 Школа диабета";
        string btnSettings = lang == "kk" ? "⚙️ Баптаулар" : "⚙️ Настройки";

        // ГЛЮКОМЕТРИЯ
        if (text == btnGlu)
        {
            await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);
            await _glucose.ShowMain(chatId, lang, ct);
            return;
        }

        // ХЛЕБНЫЕ ЕДИНИЦЫ
        if (text == btnBu)
        {
            await _state.SetPhaseAsync(userId, UserPhase.BreadUnits);
            await _bu.ShowMain(chatId, lang, ct);
            return;
        }

        // ШКОЛА ДИАБЕТА
        if (text == btnSchool)
        {
            await _state.SetPhaseAsync(userId, UserPhase.School);
            await _school.ShowMainMenuAsync(chatId, userId, ct);
            return;
        }

        // --------------------------------------------------------
        // ЛОГИКА ВНУТРИ ГЛЮКОМЕТРИИ
        // --------------------------------------------------------
        if (phase == UserPhase.GlucoseMenu)
        {
            await _glucose.HandleMessage(chatId, text, lang, ct);
            return;
        }

        // --------------------------------------------------------
        // ЛОГИКА ВНУТРИ ХЕ
        // --------------------------------------------------------
        if (phase == UserPhase.BreadUnits)
        {
            await _bu.HandleMessage(chatId, text, lang, ct);
            return;
        }

        // --------------------------------------------------------
        // ЛОГИКА ВНУТРИ ШКОЛЫ
        // --------------------------------------------------------
        if (phase == UserPhase.School)
        {
            await _school.HandleTextAsync(userId, chatId, text, ct);
            return;
        }

        // --------------------------------------------------------
        // ФОЛБЭК
        // --------------------------------------------------------
        await _bot.SendMessage(chatId,
            lang == "kk" ? "Мәзірден таңдаңыз." : "Используйте кнопки меню.",
            cancellationToken: ct);
    }
}
