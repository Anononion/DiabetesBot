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
            case "⬅️ В меню":
                {
                    Logger.Info($"[CMD] Нажата кнопка '⬅️ В меню' userId={userId}");
                    var user = await _storage.LoadAsync(userId);
                    await _state.SetPhaseAsync(userId, UserPhase.MainMenu);
                    await SendMainMenuAsync(chatId, user.Language, ct);
                    Logger.Info($"[CMD] Пользователь {userId} возвращён в главное меню");
                    return;
                }

            case "📈 Глюкометрия":
                {
                    Logger.Info($"[CMD] Нажата кнопка '📈 Глюкометрия' userId={userId}");
                    await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);
                    await _glucose.ShowMain(chatId, ct);
                    Logger.Info($"[CMD] Пользователь {userId} переведён в фазу GlucoseMenu");
                    return;
                }

            case "🍞 Хлебные единицы":
                {
                    Logger.Info($"[CMD] Нажата кнопка '🍞 Хлебные единицы' userId={userId}");
                    await _state.SetPhaseAsync(userId, UserPhase.BreadUnits);
                    await _bu.ShowMain(chatId, ct);
                    Logger.Info($"[CMD] Пользователь {userId} переведён в фазу BreadUnits");
                    return;
                }

            case "📚 Школа диабета":
                {
                    Logger.Info($"[CMD] Нажата кнопка '📚 Школа диабета' userId={userId}");
                    await _state.SetPhaseAsync(userId, UserPhase.DiabetesSchool);
                    await _school.ShowMainMenuAsync(chatId, userId, ct);
                    Logger.Info($"[CMD] Пользователь {userId} переведён в фазу DiabetesSchool и показано меню школы диабета");
                    return;
                }

            case "⚙️ Настройки":
                {
                    Logger.Info($"[CMD] Нажата кнопка '⚙️ Настройки' userId={userId}");
                    await ShowSettingsMenu(chatId, ct);
                    Logger.Info($"[CMD] Показано меню настроек для userId={userId}");
                    return;
                }

            case "🌐 Сменить язык":
                {
                    Logger.Info($"[CMD] Нажата кнопка '🌐 Сменить язык' userId={userId}");
                    await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);
                    await ShowLanguageMenuAsync(chatId, ct);
                    Logger.Info($"[CMD] Пользователь {userId} переведён в ChoosingLanguage, показано меню выбора языка");
                    return;
                }
            case "👤 Авторы":
            case "👤 Авторлар":
                {
                    await ShowAuthorsAsync(chatId, userId, ct);
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
        Logger.Info($"[CMD] SendMainMenuAsync: chatId={chatId}, lang='{lang}'");

        string msg = lang == "kk"
            ? "🏠 *Негізгі мәзір*"
            : "🏠 *Главное меню*";

        var kb = KeyboardBuilder.MainMenu();

        await _bot.SendMessage(chatId, msg, replyMarkup: kb, cancellationToken: ct);
        await _state.SetPhaseAsync(chatId, UserPhase.MainMenu);

        Logger.Info($"[CMD] Главное меню отправлено chatId={chatId}");
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
    public async Task ShowSettingsMenu(long chatId, CancellationToken ct)
    {
        Logger.Info($"[CMD] ShowSettingsMenu: chatId={chatId}");

        var kb = KeyboardBuilder.Menu(
            new[] { "🌐 Сменить язык" },
            showBack: true
        );

        await _bot.SendMessage(
            chatId,
            "Здесь будут настройки.",
            replyMarkup: kb,
            cancellationToken: ct);

        Logger.Info($"[CMD] Меню настроек отправлено chatId={chatId}");
    }

    public async Task ShowAuthorsAsync(long chatId, long userId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(userId);
        bool kz = user.Language == "kk";

        string text = kz
            ? "👤 *Авторлар*\n\n" +
              "🩺 *Медициналық сарапшы:* @Adiya_ua\n" +
              "🧑‍💻 *Жасаушы:* @Batyr_dot_bat\n"
            : "👤 *Авторы*\n\n" +
              "🩺 *Медицинский эксперт:* @Adiya_ua\n" +
              "🧑‍💻 *Разработчик:* @Batyr_dot_bat\n";

        await _bot.SendMessage(chatId, text, cancellationToken: ct);
    
        string baseDir = Path.Combine(AppContext.BaseDirectory, "Data", "authors");

        // Фото 1 — мед эксперт
        string photoMed = Path.Combine(baseDir, "author_medexpert.jpg");
        await using (var fs = File.OpenRead(photoMed))
        {
            await _bot.SendPhoto(
                chatId,
                new InputFileStream(fs, "author_medexpert.jpg"),
                caption: kz ? "Медициналық сарапшы" : "Медицинский эксперт",
                cancellationToken: ct
            );
        }

    // Фото 2 — разработчик
        string photoDev = Path.Combine(baseDir, "author_dev.jpg");
        await using (var fs = File.OpenRead(photoDev))
        {
            await _bot.SendPhoto(
                chatId,
                new InputFileStream(fs, "author_dev.jpg"),
                caption: kz ? "Жасаушы" : "Разработчик",
                cancellationToken: ct
            );
        }
    }
}




