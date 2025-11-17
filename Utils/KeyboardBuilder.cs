using Telegram.Bot.Types.ReplyMarkups;

namespace DiabetesBot.Utils;

public static class KeyboardBuilder
{
    public static ReplyKeyboardMarkup MainMenu()
    {
        return new(new[]
        {
            new KeyboardButton[] { "📈 Глюкометрия", "🍞 Хлебные единицы" },
            new KeyboardButton[] { "📚 Школа диабета", "⚙️ Настройки" }
        })
        {
            ResizeKeyboard = true
        };
    }

    public static ReplyKeyboardMarkup BackToMenu()
    {
        return new(new[]
        {
            new KeyboardButton[] { "⬅️ В меню" }
        })
        {
            ResizeKeyboard = true,
            OneTimeKeyboard = true
        };
    }

    public static ReplyKeyboardMarkup Menu(string[] buttons, bool showBack = true)
    {
        var rows = new List<List<KeyboardButton>>();

        foreach (var btn in buttons)
            rows.Add(new List<KeyboardButton> { new KeyboardButton(btn) });

        if (showBack)
            rows.Add(new List<KeyboardButton> { new KeyboardButton("⬅ Назад") });

        return new ReplyKeyboardMarkup(rows)
        {
            ResizeKeyboard = true
        };
    }

    public static InlineKeyboardMarkup List(string[] items, bool showBack = true)
    {
        var rows = items.Select(i =>
            new[] { InlineKeyboardButton.WithCallbackData(i, i) }
        ).ToList();

        if (showBack)
            rows.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅ Назад", "BACK") });

        return new InlineKeyboardMarkup(rows);
    }
}
