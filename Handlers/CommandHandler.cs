using DiabetesBot.Models;
using DiabetesBot.Services;
using DiabetesBot.Modules;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiabetesBot.Handlers;

public class CommandHandler
{
    private readonly ITelegramBotClient _bot;

    private readonly GlucoseModule _glucose;
    private readonly BreadUnitsModule _breadUnits;
    private readonly DiabetesSchoolModule _school;

    public CommandHandler(
        ITelegramBotClient bot,
        GlucoseModule glucose,
        BreadUnitsModule breadUnits,
        DiabetesSchoolModule school)
    {
        _bot = bot;
        _glucose = glucose;
        _breadUnits = breadUnits;
        _school = school;
    }

    // ============================================================
    // MAIN ENTRY FOR TEXT MESSAGES
    // ============================================================
    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        long userId = msg.From!.Id;
        long chatId = msg.Chat.Id;
        string text = msg.Text ?? "";

        BotLogger.Info($"[CMD] TEXT: '{text}' from {userId}");

        var user = StateStore.Get(userId);

        BotLogger.Info($"[CMD] User state: lang={user.Language}, phase={user.Phase}");

        // ============================================================
        // ГЛОБАЛЬНАЯ КНОПКА НАЗАД
        // ============================================================
        string globalBack = user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад";

        if (text == globalBack)
        {
            BotLogger.Info($"[CMD] GLOBAL BACK from phase={user.Phase}");

            user.Phase = BotPhase.MainMenu;
            await SendMainMenuAsync(user, chatId, ct);
            return;
        }

        // Route by phase
        switch (user.Phase)
        {
            case BotPhase.MainMenu:
                await HandleMainMenuAsync(user, chatId, text, ct);
                break;

            case BotPhase.Settings:
                await HandleSettingsAsync(user, chatId, text, ct);
                break;

            case BotPhase.LanguageChoice:
                await HandleLanguageChoiceAsync(user, chatId, text, ct);
                break;

            // ============================
            // ГЛЮКОЗА
            // ============================
            case BotPhase.Glucose:
                await _glucose.HandleTextAsync(user, chatId, text, ct);
                break;

            case BotPhase.Glucose_ValueInput:
                await _glucose.HandleValueInputAsync(user, chatId, text, ct);
                break;

            // ============================
            // ХЕ
            // ============================
            case BotPhase.BreadUnits:
                await _breadUnits.HandleTextAsync(user, chatId, text, ct);
                break;

            case BotPhase.BreadUnits_EnterGrams:
                await _breadUnits.HandleGramsInputAsync(user, chatId, text, ct);
                break;

            // ============================
            // ШКОЛА ДИАБЕТА
            // ============================
            case BotPhase.DiabetesSchool:
                await _school.HandleTextAsync(user, chatId, text, ct);
                break;

            default:
                BotLogger.Warn("[CMD] UNKNOWN PHASE → reset to MainMenu");
                user.Phase = BotPhase.MainMenu;
                await SendMainMenuAsync(user, chatId, ct);
                break;
        }
    }

    // ============================================================
    // MAIN MENU
    // ============================================================
    private async Task HandleMainMenuAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        var lang = user.Language;

        string g = lang == "kz" ? "Глюкоза📈" : "Глюкоза📈";
        string xe = lang == "kz" ? "ХЕ🍞" : "ХЕ🍞";
        string sch = lang == "kz" ? "Диабет мектебі📚" : "Школа диабета📚";
        string set = lang == "kz" ? "Баптаулар⚙️" : "Настройки⚙️";

        if (text == g)
        {
            user.Phase = BotPhase.Glucose;
            await _glucose.ShowMenuAsync(user, chatId, ct);
            return;
        }

        if (text == xe)
        {
            user.Phase = BotPhase.BreadUnits;
            await _breadUnits.ShowMenuAsync(user, chatId, ct);
            return;
        }

        if (text == sch)
        {
            user.Phase = BotPhase.DiabetesSchool;
            await _school.ShowMainMenuAsync(user, chatId, ct);
            return;
        }

        if (text == set)
        {
            user.Phase = BotPhase.Settings;
            await SendSettingsMenuAsync(user, chatId, ct);
            return;
        }

        await SendMainMenuAsync(user, chatId, ct);
    }

    // ============================================================
    // SETTINGS
    // ============================================================
    private async Task HandleSettingsAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        var lang = user.Language;

        string langBtn = lang == "kz" ? "Тіл🌐" : "Язык🌐";
        string back = lang == "kz" ? "⬅️ Артқа" : "⬅️ Назад";

        if (text == langBtn)
        {
            user.Phase = BotPhase.LanguageChoice;

            await _bot.SendMessage(chatId,
                lang == "kz" ? "Тілді таңдаңыз:" : "Выберите язык:",
                replyMarkup: KeyboardBuilder.LanguageMenu(),
                cancellationToken: ct);

            return;
        }

        if (text == back)
        {
            user.Phase = BotPhase.MainMenu;
            await SendMainMenuAsync(user, chatId, ct);
            return;
        }

        await SendSettingsMenuAsync(user, chatId, ct);
    }

    // ============================================================
    // LANGUAGE SELECT
    // ============================================================
    private async Task HandleLanguageChoiceAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text == KeyboardBuilder.LangRu)
        {
            user.Language = "ru";
        }
        else if (text == KeyboardBuilder.LangKz)
        {
            user.Language = "kz";
        }
        else
        {
            await _bot.SendMessage(chatId,
                "Выберите язык / Тілді таңдаңыз",
                replyMarkup: KeyboardBuilder.LanguageMenu(),
                cancellationToken: ct);
            return;
        }

        BotLogger.Info($"[CMD] Language set → {user.Language}");

        user.Phase = BotPhase.MainMenu;
        await SendMainMenuAsync(user, chatId, ct);
    }

    // ============================================================
    // MENU UI
    // ============================================================
    private async Task SendMainMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[CMD] SendMainMenu");

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Басты мәзір:" : "Главное меню:",
            replyMarkup: KeyboardBuilder.MainMenu(user.Language),
            cancellationToken: ct);
    }

    private async Task SendSettingsMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[CMD] SendSettingsMenu");

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Баптаулар:" : "Настройки:",
            replyMarkup: KeyboardBuilder.SettingsMenu(user.Language),
            cancellationToken: ct);
    }
}

