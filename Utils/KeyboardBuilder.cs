using Telegram.Bot.Types.ReplyMarkups;

namespace DiabetesBot.Services;

public static class KeyboardBuilder
{
    // ============================
    // LABELS
    // ============================

    public static string BtnGlucose(string lang) =>
        lang == "kz" ? "Глюкоза📈" : "Глюкоза📈";

    public static string BtnBreadUnits(string lang) =>
        lang == "kz" ? "Нан бірліктері🍞" : "ХЕ🍞";

    public static string BtnSchool(string lang) =>
        lang == "kz" ? "Қант диабеті мектебі📚" : "Школа диабета📚";

    public static string BtnSettings(string lang) =>
        lang == "kz" ? "Баптаулар⚙️" : "Настройки⚙️";

    public static string BtnBack(string lang) =>
        lang == "kz" ? "Артқа" : "Назад";

    public static string BtnLanguage(string lang) =>
        lang == "kz" ? "Тіл🌐" : "Язык🌐";

    public static string LangRu => "Русский 🇷🇺";
    public static string LangKz => "Қазақша 🇰🇿";

    // ============================
    // MAIN MENU
    // ============================

    public static IReplyMarkup MainMenu(string lang)
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                BtnGlucose(lang),
                BtnBreadUnits(lang)
            },
            new[]
            {
                BtnSchool(lang),
                BtnSettings(lang)
            }
        })
        {
            ResizeKeyboard = true
        };
    }

    // ============================
    // SETTINGS MENU
    // ============================

    public static IReplyMarkup SettingsMenu(string lang)
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                BtnLanguage(lang)
            },
            new[]
            {
                BtnBack(lang)
            }
        })
        {
            ResizeKeyboard = true
        };
    }
}
