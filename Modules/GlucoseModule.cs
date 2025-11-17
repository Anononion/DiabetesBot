using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Models;
using DiabetesBot.Services;
using DiabetesBot.Utils;

namespace DiabetesBot.Modules;

public class GlucoseModule
{
    private readonly TelegramBotClient _bot;
    private readonly JsonStorageService _storage;
    private readonly UserStateService _state;

    private static readonly Dictionary<long, string> PendingInputs = new();

    public GlucoseModule(
        TelegramBotClient bot,
        UserStateService state,
        JsonStorageService storage)
    {
        _bot = bot;
        _state = state;
        _storage = storage;
    }

    // === Главное меню ===
    public async Task ShowMain(long chatId, CancellationToken ct)
    {
        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "➕ Добавить измерение" },
            new KeyboardButton[] { "📋 История", "📊 Статистика" },
            new KeyboardButton[] { "⬅️ В меню" }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId, "Выберите действие:", replyMarkup: kb, cancellationToken: ct);
    }

    // === Обработка текстов ===
    public async Task HandleMessage(long chatId, string text, CancellationToken ct)
    {
        long userId = chatId;

        var phase = await _state.GetPhaseAsync(userId);
        if (phase != UserPhase.GlucoseMenu) return;

        switch (text)
        {
            case "➕ Добавить измерение":
                await StartMeasurementAsync(chatId, ct);
                return;

            case "📋 История":
                await ShowHistoryAsync(chatId, ct);
                return;

            case "📊 Статистика":
                await ShowStatsAsync(chatId, ct);
                return;
        }
    }

    // === Начало измерения ===
    public async Task StartMeasurementAsync(long chatId, CancellationToken ct)
    {
        var kb = new InlineKeyboardMarkup(new[]
        {
            new[] {
                InlineKeyboardButton.WithCallbackData("⏱️ Натощак", "measure_fasting"),
                InlineKeyboardButton.WithCallbackData("🍽️ После еды", "measure_after")
            },
            new[] {
                InlineKeyboardButton.WithCallbackData("⏰ По времени", "measure_time"),
                InlineKeyboardButton.WithCallbackData("❌ Забыл", "measure_skip")
            }
        });

        await _bot.SendMessage(chatId, "Выберите тип измерения:", replyMarkup: kb, cancellationToken: ct);
    }

    // === Обработка callback ===
    public async Task HandleCallbackAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Data == null || !query.Data.StartsWith("measure_"))
            return;

        long userId = query.From.Id;
        long chatId = query.Message!.Chat.Id;

        string type = query.Data.Replace("measure_", "");

        if (type == "skip")
        {
            await _bot.SendMessage(chatId, "Измерение пропущено.", cancellationToken: ct);
            return;
        }

        PendingInputs[userId] = type;
        await _state.SetPhaseAsync(userId, UserPhase.AwaitGlucoseValue);

        await _bot.SendMessage(chatId, "Введите уровень сахара (например 5.6):", cancellationToken: ct);
    }

    // === Приём текстового значения ===
    public async Task HandleValueInput(long chatId, string text, CancellationToken ct)
    {
        var msg = new Message
        {
            Chat = new Chat { Id = chatId },
            From = new User { Id = chatId },
            Text = text
        };

        await HandleTextInputAsync(msg, ct);
    }

    public async Task HandleTextInputAsync(Message msg, CancellationToken ct)
    {
        long chatId = msg.Chat.Id;
        long userId = msg.From!.Id;

        if (await _state.GetPhaseAsync(userId) != UserPhase.AwaitGlucoseValue)
            return;

        if (!PendingInputs.ContainsKey(userId))
            return;

        string type = PendingInputs[userId];
        string valueText = msg.Text!.Replace(',', '.');

        if (!double.TryParse(valueText,
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out double val))
        {
            await _bot.SendMessage(chatId, "Введите корректное число.", cancellationToken: ct);
            return;
        }

        var user = await _storage.LoadAsync(userId);

        user.Measurements.Add(new Measurement
        {
            Timestamp = DateTime.Now,
            Type = type,
            Value = val
        });

        await _storage.SaveAsync(user);
        PendingInputs.Remove(userId);
        await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);

        // → интерпретация
        string status = InterpretGlucose(val, type, user.Language);
        string advice = AdviceGlucose(val, type, user.Language);

        string reply = user.Language == "kk"
            ? $"Жазылды: *{val:F1}* ммоль/л ({type})\nҚорытынды: *{status}*\n{advice}"
            : $"Записано: *{val:F1}* ммоль/л ({type})\nСтатус: *{status}*\n{advice}";

        await _bot.SendMessage(chatId, reply, cancellationToken: ct);
        await ShowMain(chatId, ct);
    }

    // === История ===
    public async Task ShowHistoryAsync(long chatId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);

        if (user.Measurements.Count == 0)
        {
            await _bot.SendMessage(chatId, "История пуста.", cancellationToken: ct);
            return;
        }

        var list = user.Measurements
            .OrderByDescending(x => x.Timestamp)
            .Take(10);

        string text = "Последние измерения:\n\n" +
                      string.Join("\n", list.Select(x =>
                          $"{x.Timestamp:dd.MM HH:mm} — {x.Value:F1} ммоль/л ({x.Type})"));

        await _bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    // === Статистика ===
    public async Task ShowStatsAsync(long chatId, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);
        var now = DateTime.Now;

        var last7 = user.Measurements
            .Where(x => (now - x.Timestamp).TotalDays <= 7)
            .ToList();

        if (last7.Count == 0)
        {
            await _bot.SendMessage(chatId, "Нет данных за последние 7 дней.", cancellationToken: ct);
            return;
        }

        double avg = last7.Average(x => x.Value.GetValueOrDefault());
        double min = last7.Min(x => x.Value.GetValueOrDefault());
        double max = last7.Max(x => x.Value.GetValueOrDefault());


        string text =
            "📊 Статистика за 7 дней:\n" +
            $"Среднее: {avg:F1} ммоль/л\n" +
            $"Мин.: {min:F1} ммоль/л\n" +
            $"Макс.: {max:F1} ммоль/л\n" +
            $"Записей: {last7.Count}";

        await _bot.SendMessage(chatId, text, cancellationToken: ct);

        // график
        var chartBytes = ChartGenerator.GenerateGlucoseChart(last7);

        await _bot.SendPhoto(
            chatId,
            new InputFileStream(new MemoryStream(chartBytes), "glucose.png"),
            caption: "График:",
            cancellationToken: ct
        );
    }

    // === Интерпретация уровня глюкозы (ВОЗ/ADA) ===
    private string InterpretGlucose(double v, string type, string lang)
    {
        string low = lang == "kk" ? "🟡 Төмен" : "🟡 Понижено";
        string norm = lang == "kk" ? "🟢 Норма" : "🟢 Норма";
        string high = lang == "kk" ? "🟠 Жоғары" : "🟠 Повышено";
        string danger = lang == "kk" ? "🔴 Өте жоғары (гипергликемия)" : "🔴 Очень высокое (гипергликемия)";

        if (type == "fasting")
        {
            if (v < 3.9) return low;
            if (v <= 5.5) return norm;
            if (v <= 6.9) return high;
            return danger;
        }

        if (type == "after")
        {
            if (v < 3.9) return low;
            if (v <= 7.8) return norm;
            if (v <= 11.0) return high;
            return danger;
        }

        if (type == "time")
        {
            if (v < 3.9) return low;
            if (v < 11.1) return norm;
            return danger;
        }

        return norm;
    }

    // === Советы по ВОЗ/ADA ===
    private string AdviceGlucose(double v, string type, string lang)
    {
        if (v < 3.9)
            return lang == "kk"
                ? "🟡 *Гипогликемия:* тәтті шай ішіңіз немесе 15 г тез көмірсу қабылдаңыз."
                : "🟡 *Гипогликемия:* выпейте сладкий чай или примите 15 г быстрых углеводов.";

        if (v >= 11.1)
            return lang == "kk"
                ? "🔴 *Жоғары глюкоза:* су көп ішіңіз, өлшеуді қайталаңыз. Күшейсе – дәрігерге хабарласыңыз."
                : "🔴 *Высокая глюкоза:* пейте воду и повторите измерение. Если сохраняется – обратитесь к врачу.";

        return lang == "kk"
            ? "🟢 Көрсеткіш қалыпты."
            : "🟢 Значение в пределах нормы.";
    }
}
