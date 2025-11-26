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

    // ============================================================
    // TEXT HANDLER
    // ============================================================
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

        // -------------- /start ----------------
        if (text.Equals("/start", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Info($"[CMD] /start от userId={userId}");
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);
            await ShowLanguageMenuAsync(chatId, ct);
            Logger.Info($"[CMD] /start обработан: перевели в фазу ChoosingLanguage и показали меню выбора языка");
            return;
        }

        // текущая фаза
        var phase = await _state.GetPhaseAsync(userId);
        Logger.Info($"[CMD] Текущая фаза userId={userId}: {phase}");

        // пока ждём выбор языка — запрещаем текст
        if (phase == UserPhase.ChoosingLanguage)
        {
            Logger.Info($"[CMD] Пользователь {userId} в фазе ChoosingLanguage, отклоняем текст '{text}'");
            await _bot.SendMessage(chatId, "Используйте кнопки ниже для выбора языка.", cancellationToken: ct);
            return;
        }

        // ========================================================
        // ГЛОБАЛЬНЫЕ КНОПКИ, РАБОТАЮТ ИЗ ЛЮБОЙ ФАЗЫ (кроме ChoosingLanguage)
        // ========================================================
        switch (text)
        {
    // ====== НАЗАД В МЕНЮ ======
            case "⬅️ В меню":
            case "⬅ Менюге":
                {
                    var user = await _storage.LoadAsync(userId);
                    await _state.SetPhaseAsync(userId, UserPhase.MainMenu);
                    await SendMainMenuAsync(chatId, user.Language, ct);
                    return;
                }

    // ====== ГЛЮКОЗА ======
            case "📈 Глюкометрия":
            case "📈 Қант өлшеу":
                {
                    await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);
                    await _glucose.ShowMain(chatId, ct);
                    return;
                }

    // ====== ХЕ ======
                case "🍞 Хлебные единицы":
                case "🍞 НБ (нан бірлігі)":
                {
                    await _state.SetPhaseAsync(userId, UserPhase.BreadUnits);
                    await _bu.ShowMain(chatId, ct);
                    return;
                }

    // ====== ШКОЛА ======
                case "📚 Школа диабета":
                case "📚 Диабет мектебі":
                  {
                    await _state.SetPhaseAsync(userId, UserPhase.DiabetesSchool);
                    await _school.ShowMainMenuAsync(chatId, userId, ct);
                    return;
                }

    // ====== НАСТРОЙКИ ======
                case "⚙️ Настройки":
                case "⚙️ Параметрлер":
                {
                    await ShowSettingsMenu(chatId, userId, ct);
                    return;
                }

    // ====== АВТОРЫ ======
            case "👤 Авторы":
            case "👤 Авторлар":
                {
            await ShowAuthorsAsync(chatId, userId, ct);
            return;
                }

    // ====== СМЕНА ЯЗЫКА ======
            case "🌐 Сменить язык":
            case "🌐 Тілді ауыстыру":
                {
                    await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);
                    await ShowLanguageMenuAsync(chatId, ct);
                    return;
                }

    // ====== НАЗАД ======
    case "⬅ Назад":
    case "⬅ Артқа":
        {
            var user = await _storage.LoadAsync(userId);
            await _state.SetPhaseAsync(userId, UserPhase.MainMenu);
            await SendMainMenuAsync(chatId, user.Language, ct);
            return;
        }
}


        // ========================================================
        // Phase routing (обработка оставшегося текста по фазам)
        // ========================================================
        switch (phase)
        {
            case UserPhase.GlucoseMenu:
                Logger.Info($"[CMD] Phase=GlucoseMenu, передаём текст в GlucoseModule.HandleMessage; text='{text}'");
                await _glucose.HandleMessage(chatId, text, ct);
                return;

            case UserPhase.AwaitGlucoseValue:
                Logger.Info($"[CMD] Phase=AwaitGlucoseValue, передаём текст в GlucoseModule.HandleValueInput; text='{text}'");
                await _glucose.HandleValueInput(chatId, text, ct);
                return;

            case UserPhase.BreadUnits:
                Logger.Info($"[CMD] Phase=BreadUnits, передаём текст в BreadUnitsModule (HandleMessage + HandleText); text='{text}'");
                await _bu.HandleMessage(chatId, text, ct);
                await _bu.HandleText(chatId, text, ct);
                return;

            case UserPhase.DiabetesSchool:
                Logger.Info($"[CMD] Phase=DiabetesSchool, передаём текст в DiabetesSchoolModule.HandleTextAsync; text='{text}'");
                await _school.HandleTextAsync(userId, chatId, text, ct);
                return;
        }

        // ========================================================
        // fallback
        // ========================================================
        Logger.Info($"[CMD] Fallback: фаза={phase}, текст='{text}'");
        await _bot.SendMessage(chatId, "Используйте меню.", cancellationToken: ct);
    }

    // ============================================================
    // CALLBACK
    // ============================================================
    public async Task HandleCallbackAsync(CallbackQuery query, CancellationToken ct)
    {
        Logger.Info($"[CMD] HandleCallbackAsync: data='{query.Data}', userId={query.From.Id}");
        await _callbackHandler.HandleAsync(query, ct);
    }

    // ============================================================
    // MAIN MENU
    // ============================================================
    public async Task SendMainMenuAsync(long chatId, string lang, CancellationToken ct)
    {
        string msg = lang == "kk"
            ? "🏠 *Негізгі мәзір*"
            : "🏠 *Главное меню*";

        var kb = KeyboardBuilder.MainMenu(lang);

        await _bot.SendMessage(chatId, msg, replyMarkup: kb, cancellationToken: ct);
        // тут у тебя фаза по userId, но в этом методе только chatId — как и было раньше
        await _state.SetPhaseAsync(chatId, UserPhase.MainMenu);
    }

    // ============================================================
    // LANGUAGE MENU
    // ============================================================
    public async Task ShowLanguageMenuAsync(long chatId, CancellationToken ct)
    {
        Logger.Info($"[CMD] ShowLanguageMenuAsync: chatId={chatId}");

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🇷🇺 Русский", "lang_ru") },
            new[] { InlineKeyboardButton.WithCallbackData("🇰🇿 Қазақ тілі", "lang_kk") }
        });

        await _bot.SendMessage(
            chatId,
            "Выберите язык / Тілді таңдаңыз:",
            replyMarkup: kb,
            cancellationToken: ct
        );

        Logger.Info($"[CMD] Меню выбора языка отправлено chatId={chatId}");
    }

    // ============================================================
    // SETTINGS
    // ============================================================
    public async Task ShowSettingsMenu(long chatId, long userId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(userId);
        string lang = string.IsNullOrWhiteSpace(user.Language) ? "ru" : user.Language;

        string title = lang == "kk"
            ? "Параметрлер"
            : "Настройки";

        var kb = KeyboardBuilder.Menu(
            lang == "kk"
                ? new[] { "🌐 Тілді ауыстыру", "👤 Авторлар" }
                : new[] { "🌐 Сменить язык", "👤 Авторы" },
            lang,
            showBack: true
        );

        await _bot.SendMessage(chatId, title, replyMarkup: kb, cancellationToken: ct);
    }

    // ============================================================
    // AUTHORS
    // ============================================================
    public async Task ShowAuthorsAsync(long chatId, long userId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(userId);
        string lang = string.IsNullOrWhiteSpace(user.Language) ? "ru" : user.Language;

        string title = lang == "kk"
            ? "👥 *Жоба авторлары*"
            : "👥 *Авторы проекта*";

        string devDesc = lang == "kk"
            ? "👨‍💻 @Batyr_dot_bat\nЖобаның әзірлеушісі"
            : "👨‍💻 @Batyr_dot_bat\nРазработчик проекта";

        string medicDesc = lang == "kk"
            ? "👩‍⚕️ @Adiya_ua\nМедициналық эксперт\nОсы анименің ең жақсы қызы"
            : "👩‍⚕️ @Adiya_ua\nМедицинский эксперт\nЛучшая девочка этого аниме";

        // Пути картинок
        string basePath = AppContext.BaseDirectory;
        string devPath = Path.Combine(basePath, "Data", "authors", "Dev.jpg");
        string medicPath = Path.Combine(basePath, "Data", "authors", "Medic.jpg");

        // Отправляем заголовок
        await _bot.SendMessage(chatId, title, cancellationToken: ct);

        // Отправляем фото + подписи
        if (System.IO.File.Exists(devPath))
            await _bot.SendPhoto(chatId,
                InputFile.FromStream(System.IO.File.OpenRead(devPath)),
                caption: devDesc,
                cancellationToken: ct);

        if (System.IO.File.Exists(medicPath))
            await _bot.SendPhoto(chatId,
                InputFile.FromStream(System.IO.File.OpenRead(medicPath)),
                caption: medicDesc,
                cancellationToken: ct);

        // Кнопка Назад (локализованная)
        await _bot.SendMessage(
            chatId,
            lang == "kk" ? "⬅ Артқа" : "⬅️ Назад",
            replyMarkup: KeyboardBuilder.Back(lang),
            cancellationToken: ct
        );
    }
}

