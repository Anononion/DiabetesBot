using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;

namespace DiabetesBot.Utils;

public static class KeyboardBuilder
{
    // ============================
    //   ГЛАВНОЕ МЕНЮ
    // ============================

    // СТАРАЯ версия (чтобы не ломать существующий код) — по умолчанию русский
    public static ReplyKeyboardMarkup MainMenu()
        => MainMenu("ru");

    // НОВАЯ версия с языком
    public static ReplyKeyboardMarkup MainMenu(string lang)
    {
        // нормализуем язык
        lang = (lang ?? "ru").ToLowerInvariant();

        if (lang == "kk")
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "📈 Қант өлшеу", "🍞 НБ (нан бірлігі)" },
                new KeyboardButton[] { "📚 Диабет мектебі", "⚙️ Баптаулар" }
            })
            {
                ResizeKeyboard = true
            };
        }

        // русский (по умолчанию)
        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "📈 Глюкометрия", "🍞 Хлебные единицы" },
            new KeyboardButton[] { "📚 Школа диабета", "⚙️ Настройки" }
        })
        {
            ResizeKeyboard = true
        };
    }

    // ============================
    //   КНОПКА "В МЕНЮ"
    // ============================

    // старая версия — для совместимости (русский текст)
    public static ReplyKeyboardMarkup BackToMenu()
        => BackToMenu("ru");

    // новая версия с языком
    public static ReplyKeyboardMarkup BackToMenu(string lang)
    {
        lang = (lang ?? "ru").ToLowerInvariant();
        string caption = lang == "kk" ? "⬅️ Мәзірге" : "⬅️ В меню";

        return new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { caption }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };
    }

    // Альтернативное имя, если захочется:
    public static ReplyKeyboardMarkup Back(string lang) => BackToMenu(lang);

    // ============================
    //   ВЕРТИКАЛЬНОЕ МЕНЮ (Reply)
    // ============================

    // старая сигнатура (без языка) — для совместимости
    public static ReplyKeyboardMarkup Menu(string[] buttons, bool showBack = true)
        => Menu(buttons, "ru", showBack);

    // новая сигнатура с языком
    public static ReplyKeyboardMarkup Menu(string[] buttons, string lang, bool showBack = true)
    {
        lang = (lang ?? "ru").ToLowerInvariant();
        var rows = new List<List<KeyboardButton>>();

        foreach (var btn in buttons)
            rows.Add(new List<KeyboardButton> { new KeyboardButton(btn) });

        if (showBack)
        {
            string backText = lang == "kk" ? "⬅ Артқа" : "⬅ Назад";
            rows.Add(new List<KeyboardButton> { new KeyboardButton(backText) });
        }

        return new ReplyKeyboardMarkup(rows)
        {
            ResizeKeyboard = true
        };
    }

    // ============================
    //   INLINE-СПИСОК (С BACK)
    // ============================

    // старая версия — без языка (по умолчанию русский текст "⬅ Назад")
    public static InlineKeyboardMarkup List(string[] items, bool showBack = true)
        => List(items, "ru", showBack);

    // новая — с языком
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
            rows.Add(new[]
            {
                InlineKeyboardButton.WithCallbackData(backText, "BACK")
            });
        }

        return new InlineKeyboardMarkup(rows);
    }

    // ============================
    //   ВЫБОР ЯЗЫКА (INLINE)
    // ============================

    // То, чего раньше не хватало и из-за чего был CS0117 (LanguageChoice не найден)
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
