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

    public CommandHandler(
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
    }

    // ============================================================
    // Показ главного меню
    // ============================================================
    public async Task SendMainMenuAsync(long chatId, string lang, CancellationToken ct)
    {
        await _bot.SendMessage(
            chatId,
            lang == "kk" ? "Басты мәзір:" : "Главное меню:",
            replyMarkup: KeyboardBuilder.MainMenu(lang),
            cancellationToken: ct
        );
    }

    // ============================================================
    // Основная обработка текстовых сообщений
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

            await _bot.SendMessage(
                chatId,
                "Выберите язык / Тілді таңдаңыз:",
                replyMarkup: KeyboardBuilder.LanguageChoice(),
                cancellationToken: ct
            );

            return;
        }

        // --------------------------------------------------------
        // проверяем фазу
        // --------------------------------------------------------
        var phase = await _state.GetPhaseAsync(userId);


        if (phase == UserPhase.ChoosingLanguage)
        {
            await _bot.SendMessage(
                chatId,
                lang == "kk" ? "Тілді төмендегі батырмадан таңдаңыз." : "Используйте кнопки ниже.",
                cancellationToken: ct
            );
            return;
        }

        // --------------------------------------------------------
        // ввод значения глюкозы
        // --------------------------------------------------------
        if (phase == UserPhase.AwaitGlucoseValue)
        {
            await _glucose.HandleValueInput(msg, ct);
            return;
        }

        // --------------------------------------------------------
        // Кнопки главного меню
        // --------------------------------------------------------
        string btnGlu = lang == "kk" ? "📈 Қант өлшеу" : "📈 Глюкометрия";
        string btnBu = lang == "kk" ? "🍞 НБ (нан бірлігі)" : "🍞 Хлебные единицы";
        string btnSchool = lang == "kk" ? "📚 Диабет мектебі" : "📚 Школа диабета";
        string btnSettings = lang == "kk" ? "⚙️ Параметрлер" : "⚙️ Настройки";

        // ГЛЮКОМЕТРИЯ
        if (text == btnGlu)
        {
            await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);
            await _glucose.ShowMain(chatId, lang, ct);
            return;
        }

        // ХЕ
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
        // ВНУТРЕННЯЯ ЛОГИКА МОДУЛЕЙ
        // --------------------------------------------------------
        if (phase == UserPhase.GlucoseMenu)
        {
            await _glucose.HandleMessage(chatId, text, lang, ct);
            return;
        }

        if (phase == UserPhase.BreadUnits)
        {
            await _bu.HandleMessage(chatId, text, lang, ct);
            return;
        }

        if (phase == UserPhase.School)
        {
            await _school.HandleTextAsync(userId, chatId, text, ct);
            return;
        }

        // --------------------------------------------------------
        // ФОЛЛБЭК
        // --------------------------------------------------------
        await _bot.SendMessage(
            chatId,
            lang == "kk" ? "Мәзірден таңдаңыз." : "Используйте кнопки меню.",
            cancellationToken: ct
        );
    }
}
