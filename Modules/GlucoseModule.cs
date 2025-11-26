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

    // ==========================
    // Главное меню
    // ==========================
    public async Task ShowMain(long chatId, string lang, CancellationToken ct)
    {
        string t_action = lang == "kk" ? "Әрекетті таңдаңыз:" : "Выберите действие:";
        string t_add = lang == "kk" ? "➕ Өлшеу қосу" : "➕ Добавить измерение";
        string t_hist = lang == "kk" ? "📋 Тарих" : "📋 История";
        string t_stats = lang == "kk" ? "📊 Статистика" : "📊 Статистика";
        string t_back = lang == "kk" ? "⬅️ Мәзірге оралу" : "⬅️ В меню";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { t_add },
            new KeyboardButton[] { t_hist, t_stats },
            new KeyboardButton[] { t_back }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId, t_action, replyMarkup: kb, cancellationToken: ct);
    }

    // ==========================
    // Обработка текстов
    // ==========================
    public async Task HandleMessage(long chatId, string text, CancellationToken ct)
    {
        long userId = chatId;
        var user = await _storage.LoadAsync(userId);
        string lang = user.Language;

        var phase = await _state.GetPhaseAsync(userId);
        if (phase != UserPhase.GlucoseMenu) return;

        string t_add = lang == "kk" ? "➕ Өлшеу қосу" : "➕ Добавить измерение";
        string t_hist = lang == "kk" ? "📋 Тарих" : "📋 История";
        string t_stats = lang == "kk" ? "📊 Статистика" : "📊 Статистика";

        switch (text)
        {
            case var _ when text == t_add:
                await StartMeasurementAsync(chatId, lang, ct);
                return;

            case var _ when text == t_hist:
                await ShowHistoryAsync(chatId, lang, ct);
                return;

            case var _ when text == t_stats:
                await ShowStatsAsync(chatId, lang, ct);
                return;
        }
    }

    // ==========================
    // Начало измерения
    // ==========================
    public async Task StartMeasurementAsync(long chatId, string lang, CancellationToken ct)
    {
        string title = lang == "kk" ? "Өлшеу түрін таңдаңыз:" : "Выберите тип измерения:";

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[] {
                InlineKeyboardButton.WithCallbackData(lang == "kk" ? "⏱️ Аш қарынға" : "⏱️ Натощак", "measure_fasting"),
                InlineKeyboardButton.WithCallbackData(lang == "kk" ? "🍽️ Тамақтан кейін" : "🍽️ После еды", "measure_after")
            },
            new[] {
                InlineKeyboardButton.WithCallbackData(lang == "kk" ? "⏰ Уақыт бойынша" : "⏰ По времени", "measure_time"),
                InlineKeyboardButton.WithCallbackData(lang == "kk" ? "❌ Өлшемеген" : "❌ Забыл", "measure_skip")
            }
        });

        await _bot.SendMessage(chatId, title, replyMarkup: kb, cancellationToken: ct);
    }

    // ==========================
    // Callback измерения
    // ==========================
    public async Task HandleCallbackAsync(CallbackQuery query, CancellationToken ct)
    {
        if (query.Data == null || !query.Data.StartsWith("measure_"))
            return;

        long userId = query.From.Id;
        long chatId = query.Message!.Chat.Id;
        var user = await _storage.LoadAsync(userId);
        string lang = user.Language;

        string type = query.Data.Replace("measure_", "");

        if (type == "skip")
        {
            string msg = lang == "kk" ? "Өлшеу өткізіліп алды." : "Измерение пропущено.";
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

    // ==========================
    // Приём значения глюкозы
    // ==========================
    public async Task HandleTextInputAsync(Message msg, CancellationToken ct)
    {
        long userId = msg.From!.Id;
        long chatId = msg.Chat.Id;

        var user = await _storage.LoadAsync(userId);
        string lang = user.Language;

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

        // сохранение
        user.Measurements.Add(new Measurement
        {
            Timestamp = DateTime.Now,
            Type = type,
            Value = val
        });

        await _storage.SaveAsync(user);
        PendingInputs.Remove(userId);
        await _state.SetPhaseAsync(userId, UserPhase.GlucoseMenu);

        // интерпретация
        string status = InterpretGlucose(val, type, lang);
        string advice = AdviceGlucose(val, type, lang);

        string reply = lang == "kk"
            ? $"Жазылды: *{val:F1}* ммоль/л ({TypeToKz(type)})\nҚорытынды: *{status}*\n{advice}"
            : $"Записано: *{val:F1}* ммоль/л ({TypeToRu(type)})\nСтатус: *{status}*\n{advice}";

        await _bot.SendMessage(chatId, reply, cancellationToken: ct);

        await ShowMain(chatId, lang, ct);
    }

    private static string TypeToRu(string t) =>
        t switch {
            "fasting" => "натощак",
            "after" => "после еды",
            "time" => "по времени",
            _ => t
        };

    private static string TypeToKz(string t) =>
        t switch {
            "fasting" => "аш қарынға",
            "after" => "тамақтан кейін",
            "time" => "уақыт бойынша",
            _ => t
        };

    // ==========================
    // История
    // ==========================
    public async Task ShowHistoryAsync(long chatId, string lang, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);

        if (user.Measurements.Count == 0)
        {
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Тарих бос." : "История пуста.",
                cancellationToken: ct);
            return;
        }

        var list = user.Measurements
            .OrderByDescending(x => x.Timestamp)
            .Take(10);

        string title = lang == "kk" ? "Соңғы өлшеулер:\n\n" : "Последние измерения:\n\n";

        string text = title + string.Join("\n", list.Select(x =>
            $"{x.Timestamp:dd.MM HH:mm} — {x.Value:F1} ммоль/л ({(lang == "kk" ? TypeToKz(x.Type) : TypeToRu(x.Type))})"));

        await _bot.SendMessage(chatId, text, cancellationToken: ct);
    }

    // ==========================
    // Статистика
    // ==========================
    public async Task ShowStatsAsync(long chatId, string lang, CancellationToken ct)
    {
        var user = await _storage.LoadAsync(chatId);
        var now = DateTime.Now;

        var last7 = user.Measurements
            .Where(x => (now - x.Timestamp).TotalDays <= 7)
            .ToList();

        if (last7.Count == 0)
        {
            await _bot.SendMessage(chatId,
                lang == "kk" ? "Соңғы 7 күнде дерек жоқ." : "Нет данных за последние 7 дней.",
                cancellationToken: ct);
            return;
        }

        double avg = last7.Average(x => x.Value.GetValueOrDefault());
        double min = last7.Min(x => x.Value.GetValueOrDefault());
        double max = last7.Max(x => x.Value.GetValueOrDefault());

        string text = lang == "kk"
            ? $"📊 7 күндік статистика:\nОрташа: {avg:F1}\nМин.: {min:F1}\nМакс.: {max:F1}\nЗаписьтер: {last7.Count}"
            : $"📊 Статистика за 7 дней:\nСреднее: {avg:F1}\nМин.: {min:F1}\nМакс.: {max:F1}\nЗаписей: {last7.Count}";

        await _bot.SendMessage(chatId, text, cancellationToken: ct);

        // график
        var chartBytes = ChartGenerator.GenerateGlucoseChart(last7);

        await _bot.SendPhoto(
            chatId,
            new InputFileStream(new MemoryStream(chartBytes), "glucose.png"),
            caption: lang == "kk" ? "График:" : "График:",
            cancellationToken: ct
        );
    }

    // ==========================
    // Интерпретация
    // ==========================
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

        return v < 3.9 ? low : v < 11.1 ? norm : danger;
    }

    // ==========================
    // Советы
    // ==========================
    private string AdviceGlucose(double v, string type, string lang)
    {
        if (v < 3.9)
            return lang == "kk"
                ? "🟡 Гипогликемия: тәтті шай ішіңіз немесе 15 г көмірсу қабылдаңыз."
                : "🟡 Гипогликемия: выпейте сладкий чай или примите 15 г углеводов.";

        if (v >= 11.1)
            return lang == "kk"
                ? "🔴 Глюкоза жоғары: су ішіңіз, өлшеуді қайталаңыз."
                : "🔴 Глюкоза высокая: пейте воду и повторите измерение.";

        return lang == "kk" ? "🟢 Көрсеткіш қалыпты." : "🟢 Значение в норме.";
    }
}
