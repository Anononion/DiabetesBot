using DiabetesBot.Modules;
using DiabetesBot.Services;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiabetesBot.Handlers;

public class CommandHandler
{
    private readonly ITelegramBotClient _bot;
    private readonly UserStateService _state;
    private readonly GlucoseModule _glucose;
    private readonly BreadUnitsModule _breadUnits;
    private readonly DiabetesSchoolModule _school;

    public CommandHandler(
        ITelegramBotClient bot,
        UserStateService state,
        GlucoseModule glucose,
        BreadUnitsModule breadUnits,
        DiabetesSchoolModule school)
    {
        _bot = bot;
        _state = state;
        _glucose = glucose;
        _breadUnits = breadUnits;
        _school = school;
    }

    // ============================================
    // MAIN TEXT ENTRY POINT
    // ============================================

    public async Task HandleTextAsync(Message msg, CancellationToken ct)
    {
        long userId = msg.From!.Id;
        long chatId = msg.Chat.Id;
        string text = msg.Text ?? "";

        BotLogger.Info($"[CMD] Incoming text: '{text}' (user={userId})");

        string lang = await _state.GetLanguageAsync(userId);
        BotLogger.Info($"[CMD] User language = {lang}");

        var phase = await _state.GetPhaseAsync(userId);
        BotLogger.Info($"[CMD] Current PHASE = {phase}");

        switch (phase)
        {
            case UserPhase.MainMenu:
                await HandleMainMenuAsync(userId, chatId, text, lang, ct);
                break;

            case UserPhase.Settings:
                await HandleSettingsAsync(userId, chatId, text, lang, ct);
                break;

            case UserPhase.Glucose:
                await _glucose.HandleInputAsync(userId, chatId, text, lang, ct);
                break;

            case UserPhase.BreadUnits:
                await _breadUnits.HandleInputAsync(userId, chatId, text, lang, ct);
                break;

            case UserPhase.DiabetesSchool:
                await HandleDiabetesSchoolAsync(userId, chatId, text, lang, ct);
                break;

            default:
                BotLogger.Warn("[CMD] Unknown phase → sending main menu");
                await SendMainMenuAsync(userId, chatId, lang, ct);
                break;
        }
    }

    // ============================================
    // MAIN MENU HANDLING
    // ============================================

    private async Task HandleMainMenuAsync(long userId, long chatId, string text, string lang, CancellationToken ct)
    {
        if (text == KeyboardBuilder.BtnGlucose(lang))
        {
            BotLogger.Info("[CMD] ENTER → Glucose module");
            await _state.SetPhaseAsync(userId, UserPhase.Glucose);
            await _glucose.ShowStartAsync(chatId, lang, ct);
            return;
        }

        if (text == KeyboardBuilder.BtnBreadUnits(lang))
        {
            BotLogger.Info("[CMD] ENTER → BreadUnits module");
            await _state.SetPhaseAsync(userId, UserPhase.BreadUnits);
            await _breadUnits.ShowCategoriesAsync(chatId, lang, ct);
            return;
        }

        if (text == KeyboardBuilder.BtnDiabetesSchool(lang))
        {
            BotLogger.Info("[CMD] ENTER → DiabetesSchool");
            await _state.SetPhaseAsync(userId, UserPhase.DiabetesSchool);
            await _school.ShowMainMenuAsync(chatId, lang, ct);
            return;
        }

        if (text == KeyboardBuilder.BtnSettings(lang))
        {
            BotLogger.Info("[CMD] ENTER → Settings");
            await _state.SetPhaseAsync(userId, UserPhase.Settings);
            await ShowSettingsAsync(chatId, lang, ct);
            return;
        }

        BotLogger.Warn("[CMD] Text NOT recognized → show main menu");
        await SendMainMenuAsync(userId, chatId, lang, ct);
    }

    // ============================================
    // SETTINGS
    // ============================================

    private async Task HandleSettingsAsync(long userId, long chatId, string text, string lang, CancellationToken ct)
    {
        BotLogger.Info($"[CMD] HandleSettings: '{text}' lang={lang}");

        if (text == KeyboardBuilder.BtnLangRu(lang) || text == "Русский 🇷🇺")
        {
            await _state.SetLanguageAsync(userId, "ru");
            await _bot.SendMessage(chatId, "Язык изменён на русский 🇷🇺", cancellationToken: ct);
            await SendMainMenuAsync(userId, chatId, "ru", ct);
            return;
        }

        if (text == KeyboardBuilder.BtnLangKk(lang) || text == "Қазақ тілі 🇰🇿")
        {
            await _state.SetLanguageAsync(userId, "kk");
            await _bot.SendMessage(chatId, "Тiл қазақ тіліне ауыстырылды 🇰🇿", cancellationToken: ct);
            await SendMainMenuAsync(userId, chatId, "kk", ct);
            return;
        }

        if (text == KeyboardBuilder.BtnBack(lang))
        {
            await SendMainMenuAsync(userId, chatId, lang, ct);
            return;
        }

        BotLogger.Warn("[CMD] Unknown settings command → Show settings again");
        await ShowSettingsAsync(chatId, lang, ct);
    }

    private async Task ShowSettingsAsync(long chatId, string lang, CancellationToken ct)
    {
        BotLogger.Info("[CMD] ShowSettings()");
        await _bot.SendMessage(
            chatId,
            lang == "kk" ? "Баптаулар:" : "Настройки:",
            replyMarkup: KeyboardBuilder.Settings(lang),
            cancellationToken: ct
        );
    }

    // ============================================
    // DIABETES SCHOOL
    // ============================================

    private async Task HandleDiabetesSchoolAsync(long userId, long chatId, string text, string lang, CancellationToken ct)
    {
        BotLogger.Info("[CMD] DiabetesSchool → HandleText()");

        if (text == KeyboardBuilder.BtnBack(lang))
        {
            BotLogger.Info("[CMD] DS → Back to main menu");
            await SendMainMenuAsync(userId, chatId, lang, ct);
            return;
        }

        await _school.HandleTextAsync(userId, chatId, text, lang, ct);
    }

    // ============================================
    // MAIN MENU OUTPUT
    // ============================================

    public async Task SendMainMenuAsync(long userId, long chatId, string lang, CancellationToken ct)
    {
        BotLogger.Info("[CMD] SendMainMenu()");

        await _state.SetPhaseAsync(userId, UserPhase.MainMenu);

        await _bot.SendMessage(
            chatId,
            lang == "kk" ? "Басты мәзір:" : "Главное меню:",
            replyMarkup: KeyboardBuilder.MainMenu(lang),
            cancellationToken: ct
        );
    }
}
