using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
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

    private CallbackHandler _callbackHandler;

    public CommandHandler(
        TelegramBotClient bot,
        UserStateService state,
        JsonStorageService storage,
        GlucoseModule glucose,
        BreadUnitsModule bu,
        DiabetesSchoolModule school,
        CallbackHandler cbHandler)
    {
        _bot = bot;
        _state = state;
        _storage = storage;
        _glucose = glucose;
        _bu = bu;
        _school = school;
        _callbackHandler = cbHandler;

        Logger.Info("[CMD] CommandHandler создан");
    }

    // -----------------------------------------------
    // /start
    // -----------------------------------------------
    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        if (msg.Text is null)
            return;

        long chatId = msg.Chat.Id;
        long userId = msg.From!.Id;
        string text = msg.Text;

        Logger.Info($"[CMD] MSG: userid={userId}, text='{text}'");

        // --- START ---
        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);
            await ShowLanguageMenuAsync(chatId, ct);
            return;
        }

        var user = await _storage.LoadAsync(userId);
        string lang = user.Language;

        var phase = await _state.GetPhaseAsync(userId);

        // пока язык не выбран — только кнопки
        if (phase == UserPhase.ChoosingLanguage)
        {
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Тілді төмендегі батырмалардан таңдаңыз." :
                               "Используйте кнопки ниже для выбора языка.",
                cancellationToken: ct);
            return;
        }

        // -----------------------------------------------
        // Главное меню
        // -----------------------------------------------
        if (text == (lang == "kk" ? "📈 Қант өлшеу" : "📈 Глюкометрия"))
        {
            await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);
            await _glucose.ShowMain(chatId, lang, ct);
            return;
        }

        if (text == (lang == "kk" ? "🍞 НБ (нан бірлігі)" : "🍞 Хлебные единицы"))
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

        if (text == (lang == "kk" ? "⚙️ Баптаулар" : "⚙️ Настройки"))
        {
            await ShowSettingsMenu(chatId, lang, ct);
            return;
        }

        // -----------------------------------------------
        // Назад в главное меню
        // -----------------------------------------------
        if (text == (lang == "kk" ? "⬅️ Мәзірге" : "⬅️ В меню"))
        {
            await _state.SetPhaseAsync(userId, UserPhase.MainMenu);
            await SendMainMenuAsync(chatId, lang, ct);
            return;
        }

        // -----------------------------------------------
        // Глюкометрия: ввод числа
        // -----------------------------------------------
        if (phase == UserPhase.AwaitGlucoseValue)
        {
            await _glucose.HandleTextInputAsync(msg, ct);
            return;
        }

        // -----------------------------------------------
        // Хлебные единицы: ввод граммов
        // -----------------------------------------------
        if (phase == UserPhase.BreadUnits)
        {
            await _bu.HandleText(chatId, text, lang, ct);
            return;
        }

        // -----------------------------------------------
        // Школа диабета: обработка текстов
        // -----------------------------------------------
        if (phase == UserPhase.DiabetesSchool)
        {
            await _school.HandleMessageAsync(chatId, userId, text, lang, ct);
            return;
        }

        // -----------------------------------------------
        // Фолбэк
        // -----------------------------------------------
        await _bot.SendMessage(chatId,
            lang == "kk" ? "Түсініксіз команда." : "Неизвестная команда.",
            cancellationToken: ct);
    }

    // -----------------------------------------------
    // Меню выбора языка
    // -----------------------------------------------
    public async Task ShowLanguageMenuAsync(long chatId, CancellationToken ct)
    {
        var kb = new InlineKeyboardMarkup(new[]
        {
            new [] { InlineKeyboardButton.WithCallbackData("Русский 🇷🇺", "lang_ru") },
            new [] { InlineKeyboardButton.WithCallbackData("Қазақ тілі 🇰🇿", "lang_kk") }
        });

        await _bot.SendMessage(chatId,
            "Выберите язык / Тілді таңдаңыз:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // -----------------------------------------------
    // Главное меню (динамическое)
    // -----------------------------------------------
    public async Task SendMainMenuAsync(long chatId, string lang, CancellationToken ct)
    {
        string g = lang == "kk" ? "📈 Қант өлшеу" : "📈 Глюкометрия";
        string b = lang == "kk" ? "🍞 НБ (нан бірлігі)" : "🍞 Хлебные единицы";
        string s = lang == "kk" ? "📚 Диабет мектебі" : "📚 Школа диабета";
        string c = lang == "kk" ? "⚙️ Баптаулар" : "⚙️ Настройки";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[]{ g },
            new KeyboardButton[]{ b },
            new KeyboardButton[]{ s },
            new KeyboardButton[]{ c }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId,
            lang == "kk" ? "Негізгі мәзір:" : "Главное меню:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // -----------------------------------------------
    // Настройки
    // -----------------------------------------------
    private async Task ShowSettingsMenu(long chatId, string lang, CancellationToken ct)
    {
        var kb = new InlineKeyboardMarkup(new[]
        {
            new [] {
                InlineKeyboardButton.WithCallbackData(
                    lang == "kk" ? "Тілді өзгерту" : "Сменить язык", "SET_LANG")
            }
        });

        await _bot.SendMessage(chatId,
            lang == "kk" ? "Баптаулар:" : "Настройки:",
            replyMarkup: kb,
            cancellationToken: ct);
    }
}
