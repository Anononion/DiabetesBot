using Telegram.Bot.Types.ReplyMarkups;

namespace DiabetesBot.Utils;

public static class KeyboardBuilder
{
    // ------------------------------------------------------
    // MAIN MENU (LANG-DEPENDENT)
    // ------------------------------------------------------
    public static ReplyKeyboardMarkup MainMenu(string lang)
    {
        return lang == "kk"
            ? new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "📈 Қант өлшеу", "🍞 НБ (нан бірлігі)" },
                    new KeyboardButton[] { "📚 Диабет мектебі", "⚙️ Параметрлер" }
                })
                { ResizeKeyboard = true }
            : new ReplyKeyboardMarkup(new[]
                {
                    new KeyboardButton[] { "📈 Глюкометрия", "🍞 Хлебные единицы" },
                    new KeyboardButton[] { "📚 Школа диабета", "⚙️ Настройки" }
                })
                { ResizeKeyboard = true };
    }

    // ------------------------------------------------------
    // BACK BUTTON (LANG-DEPENDENT)
    // ------------------------------------------------------
    public static ReplyKeyboardMarkup Back(string lang)
    {
        string back = lang == "kk" ? "⬅ Артқа" : "⬅ Назад";

        return new(new[]
        {
            new KeyboardButton[] { back }
        })
        {
            ResizeKeyboard = true
        };
    }

    // ------------------------------------------------------
    // UNIVERSAL MENU (LANG-DEPENDENT)
    // ------------------------------------------------------
    public static ReplyKeyboardMarkup Menu(string[] buttons, string lang, bool showBack = true)
    {
        var rows = new List<List<KeyboardButton>>();

        foreach (var btn in buttons)
            rows.Add(new List<KeyboardButton> { new KeyboardButton(btn) });

        if (showBack)
            rows.Add(new List<KeyboardButton> { new KeyboardButton(lang == "kk" ? "⬅ Артқа" : "⬅ Назад") });

        return new ReplyKeyboardMarkup(rows)
        {
            ResizeKeyboard = true
        };
    }
}
