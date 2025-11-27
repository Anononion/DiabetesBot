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
            return;

        long chatId = msg.Chat.Id;
        long userId = msg.From!.Id;
        string text = msg.Text;

        var user = await _storage.LoadAsync(userId);
        string lang = user.Language ?? "ru";

        var phase = await _state.GetPhaseAsync(userId);

        // -----------------------------------------------------------------
        // WAITING FOR LANGUAGE — ignore text, wait for callback
        // -----------------------------------------------------------------
        if (phase == UserPhase.ChoosingLanguage)
            return;

        // -----------------------------------------------------------------
        // /start
        // -----------------------------------------------------------------
        if (text == "/start")
        {
            await StartAsync(chatId, userId, ct);
            return;
        }

        // -----------------------------------------------------------------
        // MAIN MENU BUTTONS
        // -----------------------------------------------------------------

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

            // В школе диабета reply-кнопки должны быть отключены
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
            await ShowSettings(chatId, lang, ct);
            return;
        }

        // -----------------------------------------------------------------
        // G L U C O S E    F L O W
        // -----------------------------------------------------------------
        if (phase == UserPhase.GlucoseMenu)
        {
            await _glucose.HandleMessage(chatId, text, lang, ct);
            return;
        }

        if (phase == UserPhase.AwaitGlucoseValue)
        {
            // Numeric input → ALWAYS remove reply keyboard
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
            await _bu.HandleText(chatId, text, lang, ct);
            return;
        }

        // -----------------------------------------------------------------
        // D I A B E T E S   S C H O O L
        // -----------------------------------------------------------------
        if (phase == UserPhase.DiabetesSchool)
        {
            // All DS UI uses reply remove
            if (text == "⬅️ В меню" || text == "🔙 Артқа")
            {
                await SendMainMenuAsync(chatId, lang, ct);
                return;
            }

            await _school.HandleTextAsync(userId, chatId, text, ct);
            return;
        }

        // -----------------------------------------------------------------
        // DEFAULT → MAIN MENU
        // -----------------------------------------------------------------
        await SendMainMenuAsync(chatId, lang, ct);
    }

    // =====================================================================
    // START
    // =====================================================================
    private async Task StartAsync(long chatId, long userId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(userId);

        if (string.IsNullOrWhiteSpace(user.Language))
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

    // =====================================================================
    // SETTINGS
    // =====================================================================
    private async Task ShowSettings(long chatId, string lang, CancellationToken ct)
    {
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
        await _state.SetPhaseAsync(chatId, UserPhase.MainMenu);

        await _bot.SendMessage(
            chatId,
            lang == "kk" ? "Басты мәзір:" : "Главное меню:",
            replyMarkup: KeyboardBuilder.MainMenu(lang),
            cancellationToken: ct
        );
    }
}
