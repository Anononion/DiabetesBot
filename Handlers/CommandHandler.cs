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

        Logger.Info("[CMD] CommandHandler создан");
    }

    // ===========================================================
    //   Главный обработчик текстовых сообщений
    // ===========================================================
    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        if (msg.Text is null)
        {
            Logger.Info("[CMD] HandleMessageAsync: msg.Text is null, игнорируем");
            return;
        }

        long chatId = msg.Chat.Id;
        long userId = msg.From!.Id;
        string text = msg.Text;

        Logger.Info($"[CMD] HandleMessageAsync: chatId={chatId}, userId={userId}, text='{text}'");

        // грузим юзера и язык
        var user = await _storage.LoadAsync(userId);
        string lang = string.IsNullOrWhiteSpace(user.Language) ? "ru" : user.Language;

        // ---------------- /start ----------------
        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Info($"[CMD] /start от userId={userId}");
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);
            await ShowLanguageMenuAsync(chatId, ct);
            Logger.Info("[CMD] /start обработан: перевели в фазу ChoosingLanguage и показали меню выбора языка");
            return;
        }

        // -------------- /menu (на всякий случай) --------------
        if (text.Equals("/menu", StringComparison.OrdinalIgnoreCase))
        {
            await _state.SetPhaseAsync(userId, UserPhase.MainMenu);
            await SendMainMenuAsync(chatId, lang, ct);
            return;
        }

        // текущая фаза
        var phase = await _state.GetPhaseAsync(userId);
        Logger.Info($"[CMD] Текущая фаза userId={userId}: {phase}");

        // пока ждём выбор языка — запрещаем текст
        if (phase == UserPhase.ChoosingLanguage)
        {
            Logger.Info($"[CMD] Пользователь {userId} в фазе ChoosingLanguage, отклоняем текст '{text}'");
            string msgText = lang == "kk"
                ? "Тілді таңдау үшін төмендегі батырмаларды пайдаланыңыз."
                : "Используйте кнопки ниже для выбора языка.";
            await _bot.SendMessage(chatId, msgText, cancellationToken: ct);
            return;
        }

        // Глобальная кнопка "Назад в меню"
        if (text == "⬅️ В меню" || text == "⬅️ Менюге")
        {
            Logger.Info($"[CMD] Глобальная кнопка назад в меню от userId={userId}");
            await _state.SetPhaseAsync(userId, UserPhase.MainMenu);
            await SendMainMenuAsync(chatId, lang, ct);
            return;
        }

        // =======================================================
        //           РАЗБОРОТКА ПО ФАЗАМ
        // =======================================================
        switch (phase)
        {
            // ---------------- ГЛАВНОЕ МЕНЮ ----------------
            case UserPhase.MainMenu:
                await HandleMainMenuAsync(userId, chatId, text, lang, ct);
                return;

            // ---------------- ГЛЮКОМЕТРИЯ -------------------
            case UserPhase.GlucoseMenu:
                await _glucose.HandleMessage(chatId, text, ct);
                return;

            case UserPhase.AwaitGlucoseValue:
                await _glucose.HandleValueInput(chatId, text, ct);
                return;

            // ---------------- ХЛЕБНЫЕ ЕДИНИЦЫ ----------------
            case UserPhase.BreadUnits:
                await _bu.HandleMessage(chatId, text, ct);
                return;

            // ---------------- ШКОЛА ДИАБЕТА -----------------
            case UserPhase.DiabetesSchool:
                // Вся логика Школы диабета идёт через callback-кнопки.
                // Текст здесь считаем ошибочным.
                {
                    string msgText = lang == "kk"
                        ? "Диабет мектебінде мәтін енгізудің орнына батырмаларды пайдаланыңыз."
                        : "В разделе «Школа диабета» используйте, пожалуйста, кнопки, а не текст.";
                    await _bot.SendMessage(chatId, msgText, cancellationToken: ct);
                    return;
                }

            default:
                Logger.Info($"[CMD] Неизвестная фаза {phase}, отправляем в главное меню");
                await _state.SetPhaseAsync(userId, UserPhase.MainMenu);
                await SendMainMenuAsync(chatId, lang, ct);
                return;
        }
    }

    // ===========================================================
    //   Обработка главного меню (фаза MainMenu)
    // ===========================================================
    private async Task HandleMainMenuAsync(
        long userId,
        long chatId,
        string text,
        string lang,
        CancellationToken ct)
    {
        // Глюкометрия
        bool isGlu =
            text == "📈 Глюкометрия" ||
            text == "📈 Қант өлшеу";

        if (isGlu)
        {
            Logger.Info($"[CMD] Переход в глюкометрию userId={userId}");
            await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);
            await _glucose.ShowMain(chatId, ct); // внутри модуля можно дернуть язык через storage при желании
            return;
        }

        // Хлебные единицы
        bool isBu =
            text == "🍞 Хлебные единицы" ||
            text == "🍞 НБ (нан бірлігі)";

        if (isBu)
        {
            Logger.Info($"[CMD] Переход в ХЕ userId={userId}");
            await _state.SetPhaseAsync(userId, UserPhase.BreadUnits);
            await _bu.ShowMain(chatId, ct);
            return;
        }

        // Школа диабета
        bool isSchool =
            text == "📚 Школа диабета" ||
            text == "📚 Диабет мектебі";

        if (isSchool)
        {
            Logger.Info($"[CMD] Переход в Школу диабета userId={userId}");
            await _state.SetPhaseAsync(userId, UserPhase.DiabetesSchool);
            await _school.ShowMainMenuAsync(chatId, userId, ct);
            return;
        }

        // Настройки
        bool isSettings =
            text == "⚙️ Настройки" ||
            text == "⚙️ Баптаулар";

        if (isSettings)
        {
            Logger.Info($"[CMD] Открыты настройки userId={userId}");
            await ShowSettingsMenuAsync(chatId, lang, ct);
            return;
        }

        // Сменить язык (из настроек)
        bool isChangeLang =
            text == "🌐 Сменить язык" ||
            text == "🌐 Тілді ауыстыру";

        if (isChangeLang)
        {
            Logger.Info($"[CMD] Сменить язык userId={userId}");
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);
            await ShowLanguageMenuAsync(chatId, ct);
            return;
        }

        // Авторы
        bool isAuthors =
            text == "👥 Авторы" ||
            text == "👥 Авторлар";

        if (isAuthors)
        {
            Logger.Info($"[CMD] Открыт раздел авторов userId={userId}");
            await ShowAuthorsAsync(chatId, lang, ct);
            return;
        }

        // Фолбэк в главном меню
        Logger.Info($"[CMD] Fallback: фаза=MainMenu, текст='{text}'");
        string fallback = lang == "kk"
            ? "Мәзірдегі батырмаларды пайдаланыңыз."
            : "Пожалуйста, используйте кнопки в меню.";
        await _bot.SendMessage(chatId, fallback, cancellationToken: ct);
    }

    // ===========================================================
    //   Главный экран (клавиатура)
    // ===========================================================
    public async Task SendMainMenuAsync(long chatId, string lang, CancellationToken ct)
    {
        var kb = BuildMainMenuKeyboard(lang);

        string text = lang == "kk"
            ? "Негізгі мәзір:"
            : "Главное меню:";

        await _bot.SendMessage(chatId, text, replyMarkup: kb, cancellationToken: ct);
    }

    private ReplyKeyboardMarkup BuildMainMenuKeyboard(string lang)
    {
        string glu = lang == "kk" ? "📈 Қант өлшеу" : "📈 Глюкометрия";
        string bu = lang == "kk" ? "🍞 НБ (нан бірлігі)" : "🍞 Хлебные единицы";
        string school = lang == "kk" ? "📚 Диабет мектебі" : "📚 Школа диабета";
        string settings = lang == "kk" ? "⚙️ Баптаулар" : "⚙️ Настройки";

        return new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(glu), new KeyboardButton(bu) },
            new[] { new KeyboardButton(school), new KeyboardButton(settings) }
        })
        {
            ResizeKeyboard = true
        };
    }

    // ===========================================================
    //   Меню выбора языка
    // ===========================================================
    private async Task ShowLanguageMenuAsync(long chatId, CancellationToken ct)
    {
        var kb = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("Русский 🇷🇺", "lang_ru"),
                InlineKeyboardButton.WithCallbackData("Қазақ тілі 🇰🇿", "lang_kk")
            }
        });

        await _bot.SendMessage(
            chatId,
            "Выберите язык / Тілді таңдаңыз:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ===========================================================
    //   Настройки
    // ===========================================================
    private async Task ShowSettingsMenuAsync(long chatId, string lang, CancellationToken ct)
    {
        string changeLang = lang == "kk" ? "🌐 Тілді ауыстыру" : "🌐 Сменить язык";
        string authors = lang == "kk" ? "👥 Авторлар" : "👥 Авторы";
        string back = lang == "kk" ? "⬅️ Менюге" : "⬅️ В меню";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(changeLang) },
            new[] { new KeyboardButton(authors) },
            new[] { new KeyboardButton(back) }
        })
        {
            ResizeKeyboard = true
        };

        string text = lang == "kk" ? "Баптаулар:" : "Настройки:";
        await _bot.SendMessage(chatId, text, replyMarkup: kb, cancellationToken: ct);
    }

    // ===========================================================
    //   Авторы (пока только текст, без фото)
    // ===========================================================
    private async Task ShowAuthorsAsync(long chatId, string lang, CancellationToken ct)
    {
        string textRu =
            "👥 *Авторы проекта Diacare*\n\n" +
            "• Медицинский эксперт и автор идеи — врач-эндокринолог.\n" +
            "• Разработчик — Batyrhan Rysbekov (архитектура бота, логика и реализация).\n\n" +
            "Бот создан как вспомогательный инструмент для людей с сахарным диабетом и не заменяет консультацию врача.";

        string textKk =
            "👥 *Diacare жобасының авторлары*\n\n" +
            "• Медициналық сарапшы және идея авторы — эндокринолог дәрігер.\n" +
            "• Әзірлеуші — Batyrhan Rysbekov (бот архитектурасы, логикасы және іске асыру).\n\n" +
            "Бот қант диабетімен өмір сүретін адамдарға көмекші құрал ретінде жасалған және дәрігер кеңесін алмастырмайды.";

        await _bot.SendMessage(chatId,
            lang == "kk" ? textKk : textRu,
            cancellationToken: ct);
    }
}
