using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
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
    }

    // =======================================================
    // ГЛАВНЫЕ ТЕКСТОВЫЕ КОМАНДЫ
    // =======================================================
    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        if (msg.Text is null)
            return;

        long chatId = msg.Chat.Id;
        long userId = msg.From!.Id;
        string text = msg.Text;

        Logger.Info($"[CMD] Msg: '{text}' от {userId}");

        // ------------------ /start ---------------------
        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);
            await ShowLanguageMenuAsync(chatId, ct);
            return;
        }

        // ------------------ /menu ----------------------
        if (text.Equals("/menu", StringComparison.OrdinalIgnoreCase))
        {
            await ShowMainMenu(chatId, ct);
            return;
        }

        // =======================================================
        // Получаем язык пользователя
        // =======================================================
        var user = await _storage.LoadAsync(userId);
        string lang = user.Language;

        // =======================================================
        // Фаза выбора языка
        // =======================================================
        var phase = await _state.GetPhaseAsync(userId);

        if (phase == UserPhase.ChoosingLanguage)
        {
            await HandleLanguageChoice(chatId, userId, text, ct);
            return;
        }

        // =======================================================
        // Главное меню (двуязычное)
        // =======================================================
        if (phase == UserPhase.MainMenu)
        {
            await HandleMainMenu(chatId, userId, text, lang, ct);
            return;
        }

        // =======================================================
        // Глюкометрия
        // =======================================================
        if (phase == UserPhase.GlucoseMenu)
        {
            await _glucose.HandleMessage(chatId, text, ct);
            return;
        }

        if (phase == UserPhase.AwaitGlucoseValue)
        {
            await _glucose.HandleTextInputAsync(msg, ct);
            return;
        }

        // =======================================================
        // Хлебные единицы
        // =======================================================
        if (phase == UserPhase.BreadUnits)
        {
            await _bu.HandleMessage(chatId, text, ct);
            await _bu.HandleText(chatId, text, ct);
            return;
        }

        // =======================================================
        // Школа диабета
        // =======================================================
        if (phase == UserPhase.SchoolMenu)
        {
            await _school.HandleMessage(chatId, text, ct);
            return;
        }

        // =======================================================
        // Фолбэк
        // =======================================================
        Logger.Info($"[CMD] Fallback: фаза={phase}, текст='{text}'");
        await _bot.SendMessage(chatId,
            lang == "kk"
                ? "Мәзірдегі батырмаларды қолданыңыз."
                : "Используйте кнопки меню.",
            cancellationToken: ct);
    }

    // =======================================================
    // ВЫБОР ЯЗЫКА
    // =======================================================
    private async Task ShowLanguageMenuAsync(long chatId, CancellationToken ct)
    {
        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "🇷🇺 Русский", "🇰🇿 Қазақша" }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId,
            "Выберите язык / Тілді таңдаңыз:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    private async Task HandleLanguageChoice(long chatId, long userId, string text, CancellationToken ct)
    {
        string? chosenLang = text switch
        {
            "🇷🇺 Русский" => "ru",
            "🇰🇿 Қазақша" => "kk",
            _ => null
        };

        if (chosenLang == null)
        {
            await _bot.SendMessage(chatId, "Выберите язык кнопками.", cancellationToken: ct);
            return;
        }

        var user = await _storage.LoadAsync(userId);
        user.Language = chosenLang;
        await _storage.SaveAsync(user);

        await _state.SetPhaseAsync(userId, UserPhase.MainMenu);
        await ShowMainMenu(chatId, ct);
    }

    // =======================================================
    // ГЛАВНОЕ МЕНЮ (двуязычное)
    // =======================================================
    private async Task ShowMainMenu(long chatId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);
        string lang = user.Language;

        string t_glucose = lang == "kk" ? "📈 Қант өлшеу" : "📈 Глюкометрия";
        string t_bu = lang == "kk" ? "🍞 НБ (нан бірлігі)" : "🍞 Хлебные единицы";
        string t_school = lang == "kk" ? "📚 Диабет мектебі" : "📚 Школа диабета";
        string t_settings = lang == "kk" ? "⚙️ Баптаулар" : "⚙️ Настройки";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { t_glucose, t_bu },
            new KeyboardButton[] { t_school, t_settings }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId,
            lang == "kk" ? "Негізгі мәзір:" : "Главное меню:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    private async Task HandleMainMenu(long chatId, long userId, string text, string lang, CancellationToken ct)
    {
        string t_glucose = lang == "kk" ? "📈 Қант өлшеу" : "📈 Глюкометрия";
        string t_bu = lang == "kk" ? "🍞 НБ (нан бірлігі)" : "🍞 Хлебные единицы";
        string t_school = lang == "kk" ? "📚 Диабет мектебі" : "📚 Школа диабета";
        string t_settings = lang == "kk" ? "⚙️ Баптаулар" : "⚙️ Настройки";

        if (text == t_glucose)
        {
            await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);
            await _glucose.ShowMain(chatId, lang, ct);
            return;
        }

        if (text == t_bu)
        {
            await _state.SetPhaseAsync(userId, UserPhase.BreadUnits);
            await _bu.ShowMain(chatId, lang, ct);
            return;
        }

        if (text == t_school)
        {
            await _state.SetPhaseAsync(userId, UserPhase.SchoolMenu);
            await _school.ShowMain(chatId, lang, ct);
            return;
        }

        if (text == t_settings)
        {
            await ShowSettings(chatId, lang, ct);
            return;
        }

        // fallback
        await _bot.SendMessage(chatId,
            lang == "kk" ? "Мәзірден таңдаңыз." : "Выберите пункт меню.",
            cancellationToken: ct);
    }

    // =======================================================
    // Настройки
    // =======================================================
    private async Task ShowSettings(long chatId, string lang, CancellationToken ct)
    {
        string t_lang = lang == "kk" ? "🌐 Тілді ауыстыру" : "🌐 Сменить язык";
        string t_auth = lang == "kk" ? "👤 Авторлар" : "👤 Авторы";
        string t_back = lang == "kk" ? "⬅️ Мәзірге" : "⬅️ В меню";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { t_lang },
            new KeyboardButton[] { t_auth },
            new KeyboardButton[] { t_back }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId,
            lang == "kk" ? "Баптаулар:" : "Настройки:",
            replyMarkup: kb,
            cancellationToken: ct);
    }
}
