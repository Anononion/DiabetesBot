using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;

namespace DiabetesBot.Utils;

public static class KeyboardBuilder
{
    // =====================================================
    //  ВОССТАНОВЛЕННЫЕ КНОПКИ (для совместимости)
    // =====================================================

    public static string Button_Glucose(string lang)
        => lang == "kk" ? "📈 Қант өлшеу" : "📈 Глюкометрия";

    public static string Button_BreadUnits(string lang)
        => lang == "kk" ? "🍞 НБ (нан бірлігі)" : "🍞 Хлебные единицы";

    public static string Button_School(string lang)
        => lang == "kk" ? "📚 Диабет мектебі" : "📚 Школа диабета";

    public static string Button_Settings(string lang)
        => lang == "kk" ? "⚙️ Баптаулар" : "⚙️ Настройки";

    public static string Button_Back(string lang)
        => lang == "kk" ? "⬅️ Мәзірге" : "⬅️ В меню";


    // =====================================================
    //  ГЛАВНОЕ МЕНЮ (новая версия)
    // =====================================================

    public static ReplyKeyboardMarkup MainMenu()
        => MainMenu("ru");

    public static ReplyKeyboardMarkup MainMenu(string lang)
    {
        lang = (lang ?? "ru").ToLowerInvariant();

        if (lang == "kk")
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { Button_Glucose(lang), Button_BreadUnits(lang) },
                new KeyboardButton[] { Button_School(lang), Button_Settings(lang) }
            })
            {
                ResizeKeyboard = true
            };
        }

        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { Button_Glucose(lang), Button_BreadUnits(lang) },
            new KeyboardButton[] { Button_School(lang), Button_Settings(lang) }
        })
        {
            ResizeKeyboard = true
        };
    }


    // =====================================================
    //  КНОПКА "НАЗАД / В МЕНЮ"
    // =====================================================

    public static ReplyKeyboardMarkup BackToMenu()
        => BackToMenu("ru");

    public static ReplyKeyboardMarkup BackToMenu(string lang)
    {
        string caption = Button_Back(lang);

        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { caption }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };
    }

    public static ReplyKeyboardMarkup Back(string lang) => BackToMenu(lang);


    // =====================================================
    //  ВЕРТИКАЛЬНОЕ МЕНЮ (ReplyKeyboard)
    // =====================================================

    public static ReplyKeyboardMarkup Menu(string[] buttons, bool showBack = true)
        => Menu(buttons, "ru", showBack);

    public static ReplyKeyboardMarkup Menu(string[] buttons, string lang, bool showBack = true)
    {
        lang = (lang ?? "ru").ToLowerInvariant();

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


    // =====================================================
    //  INLINE-СПИСОК С КНОПКОЙ "НАЗАД"
    // =====================================================

    public static InlineKeyboardMarkup List(string[] items, bool showBack = true)
        => List(items, "ru", showBack);

    public static InlineKeyboardMarkup List(string[] items, string lang, bool showBack = true)
    {
        lang = (lang ?? "ru").ToLowerInvariant();

        var rows = items.Select(i =>
            new[]
            {
                InlineKeyboardButton.WithCallbackData(i, i)
            }
        ).ToList();

        if (showBack)
        {
            string backText = lang == "kk" ? "⬅ Артқа" : "⬅ Назад";
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData(backText, "BACK") });
        }

        return new InlineKeyboardMarkup(rows);
    }


    // =====================================================
    //  ВЫБОР ЯЗЫКА
    // =====================================================

    public static InlineKeyboardMarkup LanguageChoice()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🇷🇺 Русский", "lang_ru")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData("🇰🇿 Қазақ тілі", "lang_kk")
            }
        });
    }
}
