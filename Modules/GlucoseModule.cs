using System.Globalization;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Models;
using DiabetesBot.Utils;
using System.IO;

namespace DiabetesBot.Modules;

public class GlucoseModule
{
    private readonly ITelegramBotClient _bot;

    public GlucoseModule(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    // ============================================================
    // MAIN MENU
    // ============================================================
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        string add = user.Language == "kz" ? "➕ Өлшеу қосу" : "➕ Добавить измерение";
        string history = user.Language == "kz" ? "📋 Тарих" : "📋 История";
        string stats = user.Language == "kz" ? "📊 Статистика" : "📊 Статистика";
        string back = user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { add },
            new KeyboardButton[] { history, stats },
            new KeyboardButton[] { back }
        })
        { ResizeKeyboard = true };

        string msg = user.Language == "kz" ? "Әрекетті таңдаңыз:" : "Выберите действие:";
        await _bot.SendMessage(chatId, msg, replyMarkup: kb, cancellationToken: ct);

        BotLogger.Info("[GLU] ShowMenu");
    }

    // ============================================================
    // HANDLE TEXT
    // ============================================================
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        string add = user.Language == "kz" ? "➕ Өлшеу қосу" : "➕ Добавить измерение";
        string history = user.Language == "kz" ? "📋 Тарих" : "📋 История";
        string stats = user.Language == "kz" ? "📊 Статистика" : "📊 Статистика";
        string back = user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад";

        if (text == add)
        {
            await AskMeasurementTypeAsync(user, chatId, ct);
            return;
        }

        if (text == history)
        {
            await ShowHistoryAsync(user, chatId, ct);
            return;
        }

        if (text == stats)
        {
            await ShowStatsAsync(user, chatId, ct);
            return;
        }

        if (text == back)
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        await ShowMenuAsync(user, chatId, ct);
    }

    // ============================================================
    // STEP 1 — SELECT TYPE
    // ============================================================
    private async Task AskMeasurementTypeAsync(UserData user, long chatId, CancellationToken ct)
    {
        string fasting = user.Language == "kz" ? "🕗 Ашқарын" : "🕗 Натощак";
        string after = user.Language == "kz" ? "🍽 Тамақтан кейін" : "🍽 После еды";
        string timed = user.Language == "kz" ? "⏱ Уақыт бойынша" : "⏱ По времени";
        string skip = user.Language == "kz" ? "❌ Өткізу" : "❌ Пропустить";

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[]{ InlineKeyboardButton.WithCallbackData(fasting, "GLU_TYPE|fasting") },
            new[]{ InlineKeyboardButton.WithCallbackData(after,   "GLU_TYPE|after")   },
            new[]{ InlineKeyboardButton.WithCallbackData(timed,   "GLU_TYPE|time")    },
            new[]{ InlineKeyboardButton.WithCallbackData(skip,    "GLU_SKIP")         }
        });

        string msg = user.Language == "kz" ? "Өлшеу түрін таңдаңыз:" : "Выберите тип измерения:";
        await _bot.SendMessage(chatId, msg, replyMarkup: kb, cancellationToken: ct);

        BotLogger.Info("[GLU] ask type");
    }

    // ============================================================
    // CALLBACK
    // ============================================================
    public async Task HandleCallbackAsync(UserData user, CallbackQuery q, CancellationToken ct)
    {
        string data = q.Data!;
        long chatId = q.Message!.Chat.Id;

        if (data.StartsWith("GLU_TYPE|"))
        {
            user.Phase = BotPhase.Glucose_ValueInput;
            user.TempMeasurementType = data.Split('|')[1];
            await AskValueAsync(user, chatId, ct);
            return;
        }

        if (data == "GLU_SKIP")
        {
            user.Phase = BotPhase.Glucose;
            await _bot.SendMessage(chatId, user.Language == "kz" ? "Өткізілді." : "Пропущено.", cancellationToken: ct);
            return;
        }
    }

    // ============================================================
    // STEP 2 — ENTER VALUE
    // ============================================================
    public async Task AskValueAsync(UserData user, long chatId, CancellationToken ct)
    {
        user.Phase = BotPhase.Glucose_ValueInput;

        string msg = user.Language == "kz"
            ? "Глюкоза деңгейін енгізіңіз (мысалы: 5.8):"
            : "Введите уровень глюкозы (например: 5.8):";

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);

        BotLogger.Info("[GLU] ask value");
    }

    public async Task HandleValueInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (!double.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Дұрыс сан енгізіңіз." : "Введите корректное число.",
                cancellationToken: ct);
            return;
        }

        user.Measurements.Add(new Measurement
        {
            Time = DateTime.Now,
            Type = user.TempMeasurementType!,
            Value = value
        });

        string status = InterpretGlucose(value, user.TempMeasurementType!, user.Language);
        string advice = Advice(value, user.Language);

        await _bot.SendMessage(chatId,
            $"{value:F1} ммоль/л\n{status}\n{advice}",
            cancellationToken: ct);

        user.TempMeasurementType = null;
        user.Phase = BotPhase.MainMenu;

        await ShowMenuAsync(user, chatId, ct);
    }

    // ============================================================
    // HISTORY
    // ============================================================
    private async Task ShowHistoryAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.Measurements.Count == 0)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Тарих бос." : "История пуста.",
                cancellationToken: ct);
            return;
        }

        var last = user.Measurements.OrderByDescending(x => x.Time).Take(10);

        string msg = user.Language == "kz" ? "Соңғы 10 өлшеу:\n\n" : "Последние 10:\n\n";
        msg += string.Join("\n", last.Select(x =>
            $"{x.Time:dd.MM HH:mm} — {x.Value} ммоль/л ({x.Type})"));

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }

    // ============================================================
    // STATS + GRAPH
    // ============================================================
    private async Task ShowStatsAsync(UserData user, long chatId, CancellationToken ct)
    {
        var last7 = user.Measurements.Where(x => (DateTime.Now - x.Time).TotalDays <= 7).ToList();

        if (last7.Count == 0)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "7 күнде дерек жоқ." : "Нет данных за 7 дней.",
                cancellationToken: ct);
            return;
        }

        double avg = last7.Average(x => x.Value);
        double min = last7.Min(x => x.Value);
        double max = last7.Max(x => x.Value);

        string msg = $"Среднее: {avg:F1}\nМин: {min:F1}\nМакс: {max:F1}\nЗаписей: {last7.Count}";
        await _bot.SendMessage(chatId, msg, cancellationToken: ct);

        var bytes = ChartGenerator.GenerateGlucoseChart(last7);

        await _bot.SendPhoto(
            chatId,
            new InputFileStream(new MemoryStream(bytes), "glucose.png"),
            caption: user.Language == "kz" ? "График:" : "График:",
            cancellationToken: ct
        );
    }

    // ============================================================
    // INTERPRETATION
    // ============================================================
    private string InterpretGlucose(double v, string type, string lang)
    {
        if (v < 3.9) return lang == "kz" ? "🟡 Төмен" : "🟡 Низкое";
        if (v <= 7.0) return lang == "kz" ? "🟢 Норма" : "🟢 Норма";
        if (v <= 11) return lang == "kz" ? "🟠 Жоғары" : "🟠 Повышено";
        return lang == "kz" ? "🔴 Өте жоғары" : "🔴 Очень высокое";
    }

    private string Advice(double v, string lang)
    {
        if (v < 3.9)
            return lang == "kz"
                ? "Төмен қант — тәтті шай іш."
                : "Низкий сахар — выпей сладкий чай.";

        if (v > 11)
            return lang == "kz"
                ? "Қанда қант жоғары — су іш, қайта өлшеп көр."
                : "Высокий сахар — пей воду и перепроверь.";

        return lang == "kz" ? "Қалыпты." : "Норма.";
    }
}



