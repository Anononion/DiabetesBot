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

    // =====================================================================
    // MAIN MESSAGE HANDLER
    // =====================================================================
    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        if (msg.Text is null)
        {
            BotLogger.Warn("[CMD] Message WITHOUT TEXT — ignore");
            return;
        }

        long chatId = msg.Chat.Id;
        long userId = msg.From!.Id;
        string text = msg.Text;

        BotLogger.Info($"[CMD] Incoming text: '{text}' (user={userId})");

        var user = await _storage.LoadAsync(userId);
        string lang = user.Language ?? "ru";
        BotLogger.Info($"[CMD] User language = {lang}");

        var phase = await _state.GetPhaseAsync(userId);
        BotLogger.Info($"[CMD] Current PHASE = {phase}");

        // -----------------------------------------------------------------
        // WAITING FOR LANGUAGE
        // -----------------------------------------------------------------
        if (phase == UserPhase.ChoosingLanguage)
        {
            BotLogger.Warn("[CMD] User is choosing language → ignoring TEXT (waiting for CALLBACK)");
            return;
        }

        // -----------------------------------------------------------------
        // /start
        // -----------------------------------------------------------------
        if (text == "/start")
        {
            BotLogger.Info("[CMD] /start detected");
            await StartAsync(chatId, userId, ct);
            return;
        }

        // -----------------------------------------------------------------
        // MAIN MENU BUTTONS
        // -----------------------------------------------------------------
        if (text == (lang == "kk" ? "📈 Қандағы қант" : "📈 Глюкоза"))
        {
            BotLogger.Info("[CMD] ENTER → Glucose menu");
            await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);
            await _glucose.ShowMain(chatId, lang, ct);
            return;
        }

        if (text == (lang == "kk" ? "🥖 ХЕ есептеу" : "🥖 Хлебные единицы"))
        {
            BotLogger.Info("[CMD] ENTER → Bread Units");
            await _state.SetPhaseAsync(userId, UserPhase.BreadUnits);
            await _bu.ShowMain(chatId, lang, ct);
            return;
        }

        if (text == (lang == "kk" ? "📚 Диабет мектебі" : "📚 Школа диабета"))
        {
            BotLogger.Info("[CMD] ENTER → Diabetes School");
            await _state.SetPhaseAsync(userId, UserPhase.DiabetesSchool);

            await _bot.SendMessage(
                chatId,
                lang == "kk" ? "📚 Диабет мектебі:" : "📚 Школа диабета:",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct
            );

            await _school.ShowMainMenuAsync(chatId, userId, ct);
            return;
        }

        if (text == (lang == "kk" ? "⚙️ Параметрлер" : "⚙️ Настройки"))
        {
            BotLogger.Info("[CMD] ENTER → Settings");
            await ShowSettings(chatId, lang, ct);
            return;
        }

        // -----------------------------------------------------------------
        // G L U C O S E
        // -----------------------------------------------------------------
        if (phase == UserPhase.GlucoseMenu)
        {
            BotLogger.Info("[CMD] GlucoseMenu → HandleMessage()");
            await _glucose.HandleMessage(chatId, text, lang, ct);
            return;
        }

        if (phase == UserPhase.AwaitGlucoseValue)
        {
            BotLogger.Info("[CMD] AwaitGlucoseValue → numeric input");

            await _bot.SendMessage(
                chatId,
                lang == "kk" ? "Мəлімет өңделуде..." : "Обрабатываю значение...",
                replyMarkup: new ReplyKeyboardRemove(),
                cancellationToken: ct
            );

            await _glucose.HandleValueInput(msg, ct);
            return;
        }

        // -----------------------------------------------------------------
        // B R E A D   U N I T S
        // -----------------------------------------------------------------
        if (phase == UserPhase.BreadUnits)
        {
            BotLogger.Info("[CMD] BreadUnits → HandleText()");
            await _bu.HandleText(chatId, text, lang, ct);
            return;
        }

        // -----------------------------------------------------------------
        // D I A B E T E S   S C H O O L
        // -----------------------------------------------------------------
        if (phase == UserPhase.DiabetesSchool)
        {
            BotLogger.Info("[CMD] DiabetesSchool → HandleText()");

            if (text == "⬅️ В меню" || text == "🔙 Артқа")
            {
                BotLogger.Info("[CMD] DS → Back to main menu");
                await SendMainMenuAsync(chatId, lang, ct);
                return;
            }

            await _school.HandleTextAsync(userId, chatId, text, ct);
            return;
        }

        // -----------------------------------------------------------------
        // DEFAULT
        // -----------------------------------------------------------------
        BotLogger.Warn("[CMD] Text NOT recognized → show main menu");
        await SendMainMenuAsync(chatId, lang, ct);
    }

    // =====================================================================
    // /start
    // =====================================================================
    private async Task StartAsync(long chatId, long userId, CancellationToken ct)
    {
        BotLogger.Info("[CMD] StartAsync()");

        var user = await _storage.LoadAsync(userId);

        if (string.IsNullOrWhiteSpace(user.Language))
        {
            BotLogger.Info("[CMD] User HAS NO LANGUAGE → Asking language");
            await _state.SetPhaseAsync(userId, UserPhase.ChoosingLanguage);

            await _bot.SendMessage(
                chatId,
                "Выберите язык / Тілді таңдаңыз:",
                replyMarkup: KeyboardBuilder.LanguageChoice(),
                cancellationToken: ct
            );
            return;
        }

        BotLogger.Info("[CMD] User already has language → Main Menu");
        await SendMainMenuAsync(chatId, user.Language, ct);
    }

    // =====================================================================
    // SETTINGS
    // =====================================================================
    private async Task ShowSettings(long chatId, string lang, CancellationToken ct)
    {
        BotLogger.Info("[CMD] ShowSettings()");
        await _bot.SendMessage(
            chatId,
            lang == "kk" ? "⚙️ Тілді таңдаңыз:" : "⚙️ Выберите язык:",
            replyMarkup: KeyboardBuilder.LanguageChoice(),
            cancellationToken: ct
        );
    }

    // =====================================================================
    // MAIN MENU
    // =====================================================================
    public async Task SendMainMenuAsync(long chatId, string lang, CancellationToken ct)
    {
        BotLogger.Info("[CMD] SendMainMenu()");

        await _state.SetPhaseAsync(chatId, UserPhase.MainMenu);

        await _bot.SendMessage(
            chatId,
            lang == "kk" ? "Басты мәзір:" : "Главное меню:",
            replyMarkup: KeyboardBuilder.MainMenu(lang),
            cancellationToken: ct
        );
    }
}
