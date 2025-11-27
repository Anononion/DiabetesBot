using DiabetesBot.Models;
using DiabetesBot.Services;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace DiabetesBot.Handlers;

public class CommandHandler
{
    private readonly ITelegramBotClient _bot;

    public CommandHandler(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    // =====================================================
    // MAIN ENTRY POINT
    // =====================================================

    public async Task HandleMessageAsync(Message msg, CancellationToken ct)
    {
        long userId = msg.From!.Id;
        long chatId = msg.Chat.Id;
        string text = msg.Text ?? "";

        BotLogger.Info($"[CMD] Incoming message: '{text}' (user={userId}, chat={chatId})");

        // Получаем состояние юзера
        var user = StateStore.Get(userId);

        BotLogger.Info($"[CMD] UserState: lang={user.Language}, phase={user.Phase}");

        // Роутим по фазам
        switch (user.Phase)
        {
            case BotPhase.MainMenu:
                await HandleMainMenuAsync(user, chatId, text, ct);
                break;

            case BotPhase.Glucose:
                await HandleGlucoseAsync(user, chatId, text, ct);
                break;

            case BotPhase.BreadUnits:
                await HandleBreadUnitsAsync(user, chatId, text, ct);
                break;

            case BotPhase.DiabetesSchool:
                await HandleSchoolAsync(user, chatId, text, ct);
                break;

            case BotPhase.Settings:
                await HandleSettingsAsync(user, chatId, text, ct);
                break;

            case BotPhase.LanguageChoice:
                await HandleLanguageChoiceAsync(user, chatId, text, ct);
                break;

            default:
                BotLogger.Warn("[CMD] UNKNOWN PHASE → force MainMenu");
                user.Phase = BotPhase.MainMenu;
                await SendMainMenuAsync(user, chatId, ct);
                break;
        }
    }

    // =====================================================
    // MAIN MENU
    // =====================================================

    private async Task HandleMainMenuAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        BotLogger.Info($"[CMD] MainMenu: '{text}'");

        var lang = user.Language;

        if (text == KeyboardBuilder.BtnGlucose(lang))
        {
            BotLogger.Info("[CMD] → PHASE=Glucose");
            user.Phase = BotPhase.Glucose;
            await _bot.SendMessage(chatId,
                lang == "kz" ? "Глюкоза деңгейін енгізіңіз:" : "Введите уровень глюкозы:",
                cancellationToken: ct);
            return;
        }

        if (text == KeyboardBuilder.BtnBreadUnits(lang))
        {
            BotLogger.Info("[CMD] → PHASE=BreadUnits");
            user.Phase = BotPhase.BreadUnits;
            await _bot.SendMessage(chatId,
                lang == "kz" ? "Нан бірліктерін енгізіңіз:" : "Введите количество ХЕ:",
                cancellationToken: ct);
            return;
        }

        if (text == KeyboardBuilder.BtnSchool(lang))
        {
            BotLogger.Info("[CMD] → PHASE=School");
            user.Phase = BotPhase.DiabetesSchool;
            await _bot.SendMessage(chatId,
                lang == "kz" ? "Қант диабеті мектебі бөлімі." : "Раздел школа диабета.",
                cancellationToken: ct);
            return;
        }

        if (text == KeyboardBuilder.BtnSettings(lang))
        {
            BotLogger.Info("[CMD] → PHASE=Settings");
            user.Phase = BotPhase.Settings;
            await _bot.SendMessage(chatId,
                lang == "kz" ? "Баптаулар:" : "Настройки:",
                replyMarkup: KeyboardBuilder.SettingsMenu(lang),
                cancellationToken: ct);
            return;
        }

        BotLogger.Warn("[CMD] Unknown MainMenu command → show menu");
        await SendMainMenuAsync(user, chatId, ct);
    }

    // =====================================================
    // GLUCOSE INPUT
    // =====================================================

    private async Task HandleGlucoseAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        BotLogger.Info($"[CMD] GlucoseInput: '{text}'");

        var lang = user.Language;

        if (text == KeyboardBuilder.BtnBack(lang))
        {
            user.Phase = BotPhase.MainMenu;
            await SendMainMenuAsync(user, chatId, ct);
            return;
        }

        if (!double.TryParse(text.Replace(",", "."), out double value))
        {
            BotLogger.Warn("[CMD] Invalid glucose number");
            await _bot.SendMessage(chatId,
                lang == "kz" ? "Сан енгізіңіз." : "Введите число.",
                cancellationToken: ct);
            return;
        }

        user.Measurements.Add(new Measurement
        {
            Value = value,
            Time = DateTime.Now
        });

        BotLogger.Info($"[CMD] Glucose saved: {value}");

        await _bot.SendMessage(chatId,
            lang == "kz" ? $"Жазылды: {value} ммоль/л" : $"Записано: {value} ммоль/л",
            cancellationToken: ct);

        user.Phase = BotPhase.MainMenu;
        await SendMainMenuAsync(user, chatId, ct);
    }

    // =====================================================
    // BREAD UNITS INPUT
    // =====================================================

    private async Task HandleBreadUnitsAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        BotLogger.Info($"[CMD] BreadUnitsInput: '{text}'");

        var lang = user.Language;

        if (text == KeyboardBuilder.BtnBack(lang))
        {
            user.Phase = BotPhase.MainMenu;
            await SendMainMenuAsync(user, chatId, ct);
            return;
        }

        if (!double.TryParse(text.Replace(",", "."), out double xe))
        {
            BotLogger.Warn("[CMD] Invalid XE number");
            await _bot.SendMessage(chatId,
                lang == "kz" ? "Сан енгізіңіз." : "Введите число.",
                cancellationToken: ct);
            return;
        }

        user.XeHistory.Add(new XeRecord
        {
            Value = xe,
            Time = DateTime.Now
        });

        BotLogger.Info($"[CMD] XE saved: {xe}");

        await _bot.SendMessage(chatId,
            lang == "kz" ? $"Жазылды: {xe} ХЕ" : $"Записано: {xe} ХЕ",
            cancellationToken: ct);

        user.Phase = BotPhase.MainMenu;
        await SendMainMenuAsync(user, chatId, ct);
    }

    // =====================================================
    // DIABETES SCHOOL
    // =====================================================

    private async Task HandleSchoolAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        BotLogger.Info($"[CMD] School: '{text}'");

        var lang = user.Language;

        if (text == KeyboardBuilder.BtnBack(lang))
        {
            user.Phase = BotPhase.MainMenu;
            await SendMainMenuAsync(user, chatId, ct);
            return;
        }

        await _bot.SendMessage(chatId,
            lang == "kz" ? "Бұл бөлім әзірленуде." : "Этот раздел пока в разработке.",
            cancellationToken: ct);
    }

    // =====================================================
    // SETTINGS
    // =====================================================

    private async Task HandleSettingsAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        BotLogger.Info($"[CMD] Settings: '{text}'");

        var lang = user.Language;

        if (text == KeyboardBuilder.BtnLanguage(lang))
        {
            BotLogger.Info("[CMD] Language change requested");
            user.Phase = BotPhase.LanguageChoice;

            await _bot.SendMessage(chatId,
                lang == "kz" ? "Тілді таңдаңыз:" : "Выберите язык:",
                replyMarkup: new Telegram.Bot.Types.ReplyMarkups.ReplyKeyboardMarkup(new[]
                {
                    new[] { KeyboardBuilder.LangRu, KeyboardBuilder.LangKz },
                    new[] { KeyboardBuilder.BtnBack(lang) }
                })
                {
                    ResizeKeyboard = true
                },
                cancellationToken: ct);
            return;
        }

        if (text == KeyboardBuilder.BtnBack(lang))
        {
            user.Phase = BotPhase.MainMenu;
            await SendMainMenuAsync(user, chatId, ct);
            return;
        }

        await _bot.SendMessage(chatId,
            lang == "kz" ? "Баптаулар:" : "Настройки:",
            replyMarkup: KeyboardBuilder.SettingsMenu(lang),
            cancellationToken: ct);
    }

    // =====================================================
    // LANGUAGE CHOICE
    // =====================================================

    private async Task HandleLanguageChoiceAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        BotLogger.Info($"[CMD] LanguageChoice: '{text}'");

        if (text == KeyboardBuilder.LangRu)
        {
            user.Language = "ru";
            user.Phase = BotPhase.MainMenu;

            BotLogger.Info("[CMD] Language → RU");
            await _bot.SendMessage(chatId, "Язык: Русский 🇷🇺", cancellationToken: ct);
            await SendMainMenuAsync(user, chatId, ct);
            return;
        }

        if (text == KeyboardBuilder.LangKz)
        {
            user.Language = "kk"; // Или "kz" — как хочешь
            user.Phase = BotPhase.MainMenu;

            BotLogger.Info("[CMD] Language → KZ");
            await _bot.SendMessage(chatId, "Тіл: Қазақша 🇰🇿", cancellationToken: ct);
            await SendMainMenuAsync(user, chatId, ct);
            return;
        }

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Тілді таңдаңыз:" : "Выберите язык:",
            cancellationToken: ct);
    }

    // =====================================================
    // MAIN MENU OUTPUT
    // =====================================================

    private async Task SendMainMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[CMD] SendMainMenu()");

        string msg = user.Language == "kz" ? "Басты мәзір:" : "Главное меню:";

        await _bot.SendMessage(chatId, msg,
            replyMarkup: KeyboardBuilder.MainMenu(user.Language),
            cancellationToken: ct);
    }
}
