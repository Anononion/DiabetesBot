using Telegram.Bot.Types.ReplyMarkups;

namespace DiabetesBot.Utils;

public static class KeyboardBuilder
{
    public static string LangRu => "🇷🇺 Русский";
    public static string LangKz => "🇰🇿 Қазақша";

    // ============================
    // MAIN MENU
    // ============================
    public static ReplyKeyboardMarkup MainMenu(string lang)
    {
        string g = lang == "kz" ? "Глюкоза📈" : "Глюкоза📈";
        string xe = lang == "kz" ? "ХЕ🍞" : "ХЕ🍞";
        string sch = lang == "kz" ? "Диабет мектебі📚" : "Школа диабета📚";
        string set = lang == "kz" ? "Баптаулар⚙️" : "Настройки⚙️";

        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { g },
            new KeyboardButton[] { xe },
            new KeyboardButton[] { sch },
            new KeyboardButton[] { set }
        })
        { ResizeKeyboard = true };
    }

    // ============================
    // SETTINGS
    // ============================
    public static ReplyKeyboardMarkup SettingsMenu(string lang)
    {
        string langBtn = lang == "kz" ? "Тіл🌐" : "Язык🌐";
        string back = lang == "kz" ? "⬅️ Артқа" : "⬅️ Назад";

        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { langBtn },
            new KeyboardButton[] { back }
        })
        { ResizeKeyboard = true };
    }

    // ============================
    // LANGUAGE SELECTOR
    // ============================
    public static ReplyKeyboardMarkup LanguageMenu()
    {
        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { LangRu },
            new KeyboardButton[] { LangKz }
        })
        { ResizeKeyboard = true };
    }
}
