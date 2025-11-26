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
    public async Task ShowMain(long chatId, string lang, CancellationToken ct)
    {
        var btnAdd = lang == "kk" ? "➕ Өлшеу қосу" : "➕ Добавить измерение";
        var btnHistory = lang == "kk" ? "📋 Тарих" : "📋 История";
        var btnStats = lang == "kk" ? "📊 Статистика" : "📊 Статистика";
        var btnBack = lang == "kk" ? "⬅️ Мәзірге" : "⬅️ В меню";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { btnAdd },
            new KeyboardButton[] { btnHistory, btnStats },
            new KeyboardButton[] { btnBack }
        })
        { ResizeKeyboard = true };

        string text = lang == "kk" ? "Әрекетті таңдаңыз:" : "Выберите действие:";
        await _bot.SendMessage(chatId, text, replyMarkup: kb, cancellationToken: ct);
    }

    // === Обработка текстов ===
    public async Task HandleMessage(long chatId, string text, string lang, CancellationToken ct)
    {
        long userId = chatId;

        var phase = await _state.GetPhaseAsync(userId);
        if (phase != UserPhase.GlucoseMenu) return;

        if (text == (lang == "kk" ? "➕ Өлшеу қосу" : "➕ Добавить измерение"))
        {
            await StartMeasurementAsync(chatId, lang, ct);
            return;
        }

        if (text == (lang == "kk" ? "📋 Тарих" : "📋 История"))
        {
            await ShowHistoryAsync(chatId, lang, ct);
            return;
        }

        if (text == (lang == "kk" ? "📊 Статистика" : "📊 Статистика"))
        {
            await ShowStatsAsync(chatId, lang, ct);
            return;
        }
    }

    // === Начало измерения ===
    public async Task StartMeasurementAsync(long chatId, string lang, CancellationToken ct)
    {
        string fasting = lang == "kk" ? "⏱️ Ашқарынға" : "⏱️ Натощак";
        string after = lang == "kk" ? "🍽️ Тамақтан кейін" : "🍽️ После еды";
        string time = lang == "kk" ? "⏰ Уақыт бойынша" : "⏰ По времени";
        string skip = lang == "kk" ? "❌ Ұмытып қалдым" : "❌ Забыл";

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[] {
                InlineKeyboardButton.WithCallbackData(fasting, "measure_fasting"),
                InlineKeyboardButton.WithCallbackData(after, "measure_after")
            },
            new[] {
                InlineKeyboardButton.WithCallbackData(time, "measure_time"),
                InlineKeyboardButton.WithCallbackData(skip, "measure_skip")
            }
        });

        string text = lang == "kk" ? "Өлшеу түрін таңдаңыз:" : "Выберите тип измерения:";
        await _bot.SendMessage(chatId, text, replyMarkup: kb, cancellationToken: ct);
    }

    // === Обработка callback ===
    public async Task HandleCallbackAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Data == null || !query.Data.StartsWith("measure_"))
            return;

        long userId = query.From.Id;
        long chatId = query.Message!.Chat.Id;

        string type = query.Data.Replace("measure_", "");

        var user = await _storage.LoadAsync(userId);
        string lang = user.Language ?? "ru";

        if (type == "skip")
        {
            string msg = lang == "kk" ? "Өлшеу өткізіліп алынды." : "Измерение пропущено.";
            await _bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        PendingInputs[userId] = type;
        await _state.SetPhaseAsync(userId, UserPhase.AwaitGlucoseValue);

        string ask = lang == "kk"
            ? "Қант деңгейін енгізіңіз (мысалы 5.6):"
            : "Введите уровень сахара (например 5.6):";

        await _bot.SendMessage(chatId, ask, cancellationToken: ct);
    }

    // === Приём значения ===
    public async Task HandleValueInput(Message msg, CancellationToken ct)
    {
        long chatId = msg.Chat.Id;
        long userId = msg.From!.Id;

        var user = await _storage.LoadAsync(userId);
        string lang = user.Language ?? "ru";

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
            string err = lang == "kk" ? "Дұрыс сан енгізіңіз." : "Введите корректное число.";
            await _bot.SendMessage(chatId, err, cancellationToken: ct);
            return;
        }

        user.Measurements.Add(new Measurement
        {
            Timestamp = DateTime.Now,
            Type = type,
            Value = val
        });

        await _storage.SaveAsync(user);
        PendingInputs.Remove(userId);
        await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);

        string status = InterpretGlucose(val, type, lang);
        string advice = AdviceGlucose(val, type, lang);

        string reply = lang == "kk"
            ? $"Жазылды: *{val:F1}* ммоль/л ({type})\nҚорытынды: *{status}*\n{advice}"
            : $"Записано: *{val:F1}* ммоль/л ({type})\nСтатус: *{status}*\n{advice}";

        await _bot.SendMessage(chatId, reply, cancellationToken: ct);
        await ShowMain(chatId, lang, ct);
    }

    // === История ===
    public async Task ShowHistoryAsync(long chatId, string lang, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);

        if (user.Measurements.Count == 0)
        {
            string msg = lang == "kk" ? "Тарих бос." : "История пуста.";
            await _bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        var list = user.Measurements
            .OrderByDescending(x => x.Timestamp)
            .Take(10);

        string header = lang == "kk" ? "Соңғы өлшеулер:\n\n" : "Последние измерения:\n\n";

        string text = header +
                      string.Join("\n", list.Select(x =>
                          $"{x.Timestamp:dd.MM HH:mm} — {x.Value:F1} ммоль/л ({x.Type})"));

        await _bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    // === Статистика ===
    public async Task ShowStatsAsync(long chatId, string lang, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);
        var now = DateTime.Now;

        var last7 = user.Measurements
            .Where(x => (now - x.Timestamp).TotalDays <= 7)
            .ToList();

        if (last7.Count == 0)
        {
            string msg = lang == "kk"
                ? "Соңғы 7 күнде дерек жоқ."
                : "Нет данных за последние 7 дней.";

            await _bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }

        double avg = last7.Average(x => x.Value.GetValueOrDefault());
        double min = last7.Min(x => x.Value.GetValueOrDefault());
        double max = last7.Max(x => x.Value.GetValueOrDefault());

        string txt = lang == "kk"
            ? $"📊 7 күндік статистика:\nОрташа: {avg:F1} ммоль/л\nМин.: {min:F1}\nМакс.: {max:F1}\nЖазбалар: {last7.Count}"
            : $"📊 Статистика за 7 дней:\nСреднее: {avg:F1} ммоль/л\nМин.: {min:F1}\nМакс.: {max:F1}\nЗаписей: {last7.Count}";

        await _bot.SendMessage(chatId, txt, cancellationToken: ct);

        // график
        var chartBytes = ChartGenerator.GenerateGlucoseChart(last7);

        await _bot.SendPhoto(
            chatId,
            new InputFileStream(new MemoryStream(chartBytes), "glucose.png"),
            caption: lang == "kk" ? "График:" : "График:",
            cancellationToken: ct
        );
    }

    // === Интерпретация ===
    private string InterpretGlucose(double v, string type, string lang)
    {
        string low = lang == "kk" ? "🟡 Төмен" : "🟡 Понижено";
        string norm = lang == "kk" ? "🟢 Норма" : "🟢 Норма";
        string high = lang == "kk" ? "🟠 Жоғары" : "🟠 Повышено";
        string danger = lang == "kk" ? "🔴 Өте жоғары" : "🔴 Очень высокое";

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

    // === Совет ===
    private string AdviceGlucose(double v, string type, string lang)
    {
        if (v < 3.9)
            return lang == "kk"
                ? "🟡 *Гипогликемия:* тәтті шай ішіңіз немесе 15 г жылдам көмірсу қабылдаңыз."
                : "🟡 *Гипогликемия:* выпейте сладкий чай или примите 15 г быстрых углеводов.";

        if (v >= 11.1)
            return lang == "kk"
                ? "🔴 *Жоғары глюкоза:* су ішіңіз, қайта өлшеңіз. Күшейсе — дәрігерге қаралыңыз."
                : "🔴 *Высокая глюкоза:* пейте воду, повторите измерение. Если сохраняется — обратитесь к врачу.";

        return lang == "kk"
            ? "🟢 Көрсеткіш қалыпты."
            : "🟢 Значение в пределах нормы.";
    }
}
