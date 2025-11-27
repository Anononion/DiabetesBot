using System.IO;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Models;
using DiabetesBot.Services;
using DiabetesBot.Utils;
using System.Globalization;

namespace DiabetesBot.Modules;

public class GlucoseModule
{
    private readonly ITelegramBotClient _bot;

    public GlucoseModule(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    // =====================================================
    // MAIN MENU (для раздела "Глюкоза")
    // =====================================================

    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[GLUCOSE] ShowMenu");

        string add = user.Language == "kz" ? "➕ Өлшеу қосу" : "➕ Добавить измерение";
        string history = user.Language == "kz" ? "📋 Тарих" : "📋 История";
        string stats = user.Language == "kz" ? "📊 Статистика" : "📊 Статистика";
        string back = user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(add) },
            new[] { new KeyboardButton(history), new KeyboardButton(stats) },
            new[] { new KeyboardButton(back) }
        }) { ResizeKeyboard = true };

        string msg = user.Language == "kz" ? "Әрекетті таңдаңыз:" : "Выберите действие:";
        await _bot.SendMessage(chatId, msg, replyMarkup: kb, cancellationToken: ct);
    }

    // =====================================================
    // HANDLE TEXT
    // =====================================================

    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        BotLogger.Info($"[GLUCOSE] HandleText: '{text}'");

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

        // неизвестная команда -> просто меню
        await ShowMenuAsync(user, chatId, ct);
    }

    // =====================================================
    // STEP 1 — выбрать тип измерения
    // =====================================================

    private async Task AskMeasurementTypeAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[GLUCOSE] Asking measurement type");

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

        string text = user.Language == "kz"
            ? "Өлшеу түрін таңдаңыз:"
            : "Выберите тип измерения:";

        await _bot.SendMessage(chatId, text, replyMarkup: kb, cancellationToken: ct);
    }

    // =====================================================
    // CALLBACK — выбор типа
    // =====================================================

    public async Task HandleCallbackAsync(UserData user, CallbackQuery q, CancellationToken ct)
    {
        string data = q.Data!;
        long chatId = q.Message!.Chat.Id;

        BotLogger.Info($"[GLUCOSE] Callback: {data}");

        if (data.StartsWith("GLU_TYPE|"))
        {
            string type = data.Split('|')[1];
            user.TempMeasurementType = type;

            await AskValueAsync(user, chatId, ct);
            return;
        }

        if (data == "GLU_SKIP")
        {
            string msg = user.Language == "kz"
                ? "Өлшеу өткізіліп алынды."
                : "Измерение пропущено.";

            await _bot.SendMessage(chatId, msg, cancellationToken: ct);
            return;
        }
    }

    // =====================================================
    // STEP 2 — запросить значение
    // =====================================================

    private async Task AskValueAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[GLUCOSE] Asking input value");

        user.Phase = BotPhase.Glucose_ValueInput;

        string msg = user.Language == "kz"
            ? "Глюкоза деңгейін енгізіңіз (мысалы 5.8):"
            : "Введите уровень глюкозы (например 5.8):";

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }

    // =====================================================
    // HANDLE VALUE INPUT
    // =====================================================

    public async Task HandleValueInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        BotLogger.Info($"[GLUCOSE] HandleValueInput: '{text}'");

        if (string.IsNullOrWhiteSpace(user.TempMeasurementType))
        {
            BotLogger.Warn("[GLUCOSE] No temp type set ???");
            await ShowMenuAsync(user, chatId, ct);
            return;
        }

        if (!double.TryParse(text.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out double value))
        {
            string err = user.Language == "kz" ? "Дұрыс сан енгізіңіз." : "Введите корректное число.";
            await _bot.SendMessage(chatId, err, cancellationToken: ct);
            return;
        }

        // сохранить измерение
        user.Measurements.Add(new Measurement
        {
            Time = DateTime.Now,
            Type = user.TempMeasurementType,
            Value = value
        });

        BotLogger.Info($"[GLUCOSE] Saved measurement: {value} ({user.TempMeasurementType})");

        string status = InterpretGlucose(value, user.TempMeasurementType, user.Language);
        string advice = AdviceGlucose(value, user.TempMeasurementType, user.Language);

        string msg = user.Language == "kz"
            ? $"Жазылды: *{value:F1}* ммоль/л ({user.TempMeasurementType})\n{status}\n{advice}"
            : $"Записано: *{value:F1}* ммоль/л ({user.TempMeasurementType})\n{status}\n{advice}";

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);

        // очистить временное значение
        user.TempMeasurementType = null;
        user.Phase = BotPhase.MainMenu;

        await ShowMenuAsync(user, chatId, ct);
    }


    // =====================================================
    // HISTORY
    // =====================================================

    public async Task ShowHistoryAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[GLUCOSE] ShowHistory");

        if (user.Measurements.Count == 0)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Тарих бос." : "История пуста.",
                cancellationToken: ct);
            return;
        }

        var last = user.Measurements
            .OrderByDescending(x => x.Time)
            .Take(10)
            .ToList();

        string header = user.Language == "kz" ? "Соңғы өлшеулер:\n\n" : "Последние измерения:\n\n";

        string msg = header +
                     string.Join("\n",
                         last.Select(x =>
                             $"{x.Time:dd.MM HH:mm} — {x.Value:F1} ммоль/л ({x.Type})"));

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }

    // =====================================================
    // STATS + GRAPH
    // =====================================================

    public async Task ShowStatsAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[GLUCOSE] ShowStats");

        var now = DateTime.Now;
        var last7 = user.Measurements
            .Where(x => (now - x.Time).TotalDays <= 7)
            .ToList();

        if (last7.Count == 0)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz"
                    ? "Соңғы 7 күнде дерек жоқ."
                    : "Нет данных за последние 7 дней.",
                cancellationToken: ct);
            return;
        }

        double avg = last7.Average(x => x.Value);
        double min = last7.Min(x => x.Value);
        double max = last7.Max(x => x.Value);

        string msg = user.Language == "kz"
            ? $"📊 7 күндік статистика:\nОрташа: {avg:F1}\nМин.: {min:F1}\nМакс.: {max:F1}\nЖазбалар: {last7.Count}"
            : $"📊 Статистика за 7 дней:\nСреднее: {avg:F1}\nМин.: {min:F1}\nМакс.: {max:F1}\nЗаписей: {last7.Count}";

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);

        // график
        var bytes = ChartGenerator.GenerateGlucoseChart(last7);

        await _bot.SendPhoto(
            chatId,
            new Telegram.Bot.Types.InputFiles.InputFileStream(new MemoryStream(bytes), "glucose.png"),
            caption: user.Language == "kz" ? "График:" : "График:",
            cancellationToken: ct);
    }

    // =====================================================
    // ИНТЕРПРЕТАЦИЯ
    // =====================================================

    private string InterpretGlucose(double v, string type, string lang)
    {
        if (lang == "kz")
        {
            if (v < 3.9) return "🟡 Төмен";
            if (v <= 7) return "🟢 Норма";
            if (v <= 11) return "🟠 Жоғары";
            return "🔴 Өте жоғары";
        }
        else
        {
            if (v < 3.9) return "🟡 Низко";
            if (v <= 7) return "🟢 Норма";
            if (v <= 11) return "🟠 Повышено";
            return "🔴 Очень высокое";
        }
    }

    private string AdviceGlucose(double v, string type, string lang)
    {
        if (v < 3.9)
            return lang == "kz"
                ? "Гипогликемия: тәтті шай ішіңіз немесе 15 г жылдам көмірсу қабылдаңыз."
                : "Гипогликемия: выпейте сладкий чай или примите 15 г быстрых углеводов.";

        if (v >= 11)
            return lang == "kz"
                ? "Жоғары глюкоза: су ішіңіз, қайта тексеріңіз. Күшейсе — дәрігерге барыңыз."
                : "Высокая глюкоза: пейте воду и перепроверьте. Если не снижается — обратитесь к врачу.";

        return lang == "kz"
            ? "Көрсеткіш қалыпты."
            : "Показатель в норме.";
    }
}

