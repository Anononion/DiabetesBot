using System.Globalization;
using DiabetesBot.Models;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiabetesBot.Modules;

public class GlucoseModule
{
    private readonly ITelegramBotClient _bot;

    public GlucoseModule(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    // --------------------------------------------------------------------
    // МЕНЮ ГЛЮКОЗЫ
    // --------------------------------------------------------------------
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { new("📋 История"), new("📊 Статистика") },
            new KeyboardButton[] { new("➕ Добавить измерение") },
            new KeyboardButton[] { new(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад") }
        })
        {
            ResizeKeyboard = true
        };

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Глюкоза мәзірі:" : "Меню глюкозы:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // --------------------------------------------------------------------
    // ОБРАБОТКА ТЕКСТА (фаза Glucose)
    // --------------------------------------------------------------------
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text.Contains("Назад") || text.Contains("Артқа"))
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        if (text.Contains("История"))
        {
            await SendHistoryAsync(user, chatId, ct);
            return;
        }

        if (text.Contains("Статистика"))
        {
            await SendStatsAsync(user, chatId, ct);
            return;
        }

        if (text.Contains("Добавить"))
        {
            user.Phase = BotPhase.Glucose_ValueInput;
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Мәнді енгізіңіз:" : "Введите значение:",
                cancellationToken: ct);
            return;
        }

        await ShowMenuAsync(user, chatId, ct);
    }

    // --------------------------------------------------------------------
    // ВВОД ЗНАЧЕНИЯ
    // --------------------------------------------------------------------
    public async Task HandleValueInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        var normalized = text.Replace(',', '.');

        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Сан енгізіңіз!" : "Введите число!",
                cancellationToken: ct);
            return;
        }

        user.PendingGlucoseValue = val;
        user.Phase = BotPhase.Glucose_ValueInputType;

        await AskTypeAsync(user, chatId, ct);
    }

    // --------------------------------------------------------------------
    // СПРОСИТЬ ТИП ИЗМЕРЕНИЯ
    // --------------------------------------------------------------------
    private async Task AskTypeAsync(UserData user, long chatId, CancellationToken ct)
    {
        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[]
            {
                new(user.Language=="ru" ? "🕒 Натощак" : "🕒 Ашқарын"),
                new(user.Language=="ru" ? "🍽 После еды" : "🍽 Тамақтан соң")
            },
            new KeyboardButton[]
            {
                new(user.Language=="ru" ? "⏱ По времени" : "⏱ Уақыт бойынша")
            },
            new KeyboardButton[]
            {
                new(user.Language=="ru" ? "❌ Отмена" : "❌ Болдырмау")
            }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Өлшеу түрін таңдаңыз:" : "Выберите тип измерения:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // --------------------------------------------------------------------
    // ОБРАБОТКА ТИПА (ТЕКСТ!)
    // --------------------------------------------------------------------
    public async Task HandleTypeText(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text.Contains("Отмена") || text.Contains("Болдырмау"))
        {
            user.Phase = BotPhase.Glucose;
            await ShowMenuAsync(user, chatId, ct);
            return;
        }

        string type = text switch
        {
            { } t when t.Contains("Натощак") || t.Contains("Ашқарын") => "fasting",
            { } t when t.Contains("После") || t.Contains("Тамақ") => "after",
            { } t when t.Contains("По времени") || t.Contains("Уақыт") => "time",
            _ => ""
        };

        if (string.IsNullOrEmpty(type))
        {
            await AskTypeAsync(user, chatId, ct);
            return;
        }

        user.TempGlucoseType = type;

        double value = user.PendingGlucoseValue ?? 0;

        user.Glucose.Add(new GlucoseRecord
        {
            Value = value,
            Type = type,
            Time = DateTime.UtcNow
        });

        string status = Interpret(value, type, user.Language);
        string advice = Advice(value, type, user.Language);

        string reply = user.Language == "kz"
            ? $"Жазылды: *{value:F1}* ммоль/л\nСтатус: *{status}*\n{advice}"
            : $"Записано: *{value:F1}* ммоль/л\nСтатус: *{status}*\n{advice}";

        await _bot.SendMessage(chatId, reply,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            cancellationToken: ct);

        user.Phase = BotPhase.Glucose;
        await ShowMenuAsync(user, chatId, ct);
    }

    // --------------------------------------------------------------------
    // ИНТЕРПРЕТАЦИЯ
    // --------------------------------------------------------------------
    private string Interpret(double v, string type, string lang)
    {
        bool ru = lang == "ru";

        if (type == "after")
        {
            if (v < 4.0) return ru ? "Слишком низкий" : "Төмен";
            if (v <= 7.8) return ru ? "Норма" : "Қалыпты";
            if (v <= 11) return ru ? "Повышенный" : "Жоғары";
            return ru ? "Очень высокий" : "Өте жоғары";
        }
        else
        {
            if (v < 3.5) return ru ? "Слишком низкий" : "Төмен";
            if (v <= 5.5) return ru ? "Норма" : "Қалыпты";
            if (v <= 7.0) return ru ? "Повышенный" : "Жоғары";
            return ru ? "Очень высокий" : "Өте жоғары";
        }
    }

    // --------------------------------------------------------------------
    // СОВЕТЫ
    // --------------------------------------------------------------------
    private string Advice(double v, string type, string lang)
    {
        bool ru = lang == "ru";

        if (type == "after")
        {
            if (v < 4.0) return ru ? "⚠️ Возможная гипогликемия." : "⚠️ Гипогликемия болуы мүмкін.";
            if (v <= 7.8) return ru ? "✔ Отличный результат." : "✔ Жақсы нәтиже.";
            if (v <= 11) return ru ? "⚠️ Контроль питания нужен." : "⚠️ Тамақтануды бақылаңыз.";
            return ru ? "❗ Очень высокий уровень!" : "❗ Өте жоғары деңгей!";
        }
        else
        {
            if (v < 3.5) return ru ? "⚠️ Гипогликемия!" : "⚠️ Гипо!";
            if (v <= 5.5) return ru ? "✔ Отлично." : "✔ Жақсы.";
            if (v <= 7.0) return ru ? "⚠️ Чуть выше нормы." : "⚠️ Сәл жоғары.";
            return ru ? "❗ Может быть гипергликемия!" : "❗ Гипергликемия мүмкін!";
        }
    }

    // --------------------------------------------------------------------
    // ИСТОРИЯ
    // --------------------------------------------------------------------
    private async Task SendHistoryAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.Glucose.Count == 0)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Өлшеулер жоқ." : "Нет измерений.",
                cancellationToken: ct);
            return;
        }

        string msg = string.Join(
            "\n",
            user.Glucose
                .OrderByDescending(x => x.Time)
                .Take(10)
                .Select(x =>
                {
                    string type = x.Type switch
                    {
                        "fasting" => "натощак",
                        "after" => "после еды",
                        "time" => "по времени",
                        _ => ""
                    };
                    return $"{x.Time.ToLocalTime():dd.MM HH:mm} — {x.Value:0.0} ({type})";
                })
        );

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }

    // --------------------------------------------------------------------
    // СТАТИСТИКА
    // --------------------------------------------------------------------
    private async Task SendStatsAsync(UserData user, long chatId, CancellationToken ct)
{
    if (user.Glucose.Count == 0)
    {
        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Статистика жоқ." : "Статистики нет.",
            cancellationToken: ct);
        return;
    }

    var values = user.Glucose.Select(x => x.Value).ToList();

    double avg = values.Average();
    double max = values.Max();
    double min = values.Min();

    string msg;

    if (user.Language == "kz")
    {
        msg =
            $"📊 *Статистика*\n" +
            $"Орташа мән: *{avg:0.0}*\n" +
            $"Жоғары мән: *{max:0.0}*\n" +
            $"Төмен мән: *{min:0.0}*";
    }
    else
    {
        msg =
            $"📊 *Статистика*\n" +
            $"Среднее значение: *{avg:0.0}*\n" +
            $"Максимум: *{max:0.0}*\n" +
            $"Минимум: *{min:0.0}*";
    }

    await _bot.SendMessage(chatId, msg, parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown, cancellationToken: ct);
}

}

