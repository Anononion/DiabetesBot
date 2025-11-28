using System.Globalization;
using System.Linq;
using DiabetesBot.Models;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiabetesBot.Modules;

public class GlucoseModule
{
    private readonly ITelegramBotClient _bot;

    public GlucoseModule(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    // =========================================================
    // Главное меню глюкозы
    // =========================================================
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "📋 История", "📊 Статистика" },
            new KeyboardButton[] { "➕ Добавить измерение" },
            new KeyboardButton[] { user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад" }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Глюкоза мәзірі:" : "Меню глюкозы:",
            replyMarkup: kb, cancellationToken: ct);
    }

    // =========================================================
    // Принимаем текст, когда user.Phase == Glucose
    // =========================================================
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
            await AskValueAsync(user, chatId, ct);
            return;
        }

        await ShowMenuAsync(user, chatId, ct);
    }

    // =========================================================
    // Показываем запрос на ввод числового значения
    // =========================================================
    public async Task AskValueAsync(UserData user, long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId,
            user.Language == "kz"
                ? "Мәнді енгізіңіз:"
                : "Введите значение глюкозы:",
            cancellationToken: ct);
    }

    // =========================================================
    // Принимаем значение глюкозы → переходим к выбору типа
    // =========================================================
    public async Task HandleValueInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        var normalized = text.Replace(',', '.');

        if (!double.TryParse(normalized, NumberStyles.Float,
                CultureInfo.InvariantCulture, out double value))
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Сан енгізіңіз!" : "Введите число!",
                cancellationToken: ct);
            return;
        }

        user._tempGlucoseValue = value;
        user.Phase = BotPhase.Glucose_ValueInputType;

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Өлшеу түрін таңдаңыз:" : "Выберите тип измерения:",
            replyMarkup: BuildTypeKeyboard(user),
            cancellationToken: ct);
    }

    // =========================================================
    // Inline-кнопки выбор типа
    // =========================================================
    public async Task HandleCallbackAsync(UserData user, CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Data == null || cb.Message == null)
            return;

        string data = cb.Data;

        if (!data.StartsWith("GLU_TYPE:"))
            return;

        string type = data.Split(':')[1]; // fasting / after / time / skip

        double val = user._tempGlucoseValue;

        string typeTextRu = type switch
        {
            "fasting" => "натощак",
            "after" => "после еды",
            "time" => "по времени",
            _ => "без типа"
        };

        string typeTextKz = type switch
        {
            "fasting" => "ашқарын",
            "after" => "тамақтан соң",
            "time" => "уақыт бойынша",
            _ => "түрсіз"
        };

        string typeText = user.Language == "kz" ? typeTextKz : typeTextRu;

        // Сохраняем запись
        user.Glucose.Add(new GlucoseRecord
        {
            Value = val,
            Type = typeText,
            Time = DateTime.UtcNow
        });

        user._tempGlucoseValue = 0;
        user.Phase = BotPhase.Glucose;

        // Интерпретация + совет
        string status = InterpretGlucose(val, type, user.Language);
        string advice = AdviceGlucose(val, type, user.Language);

        string msg = user.Language == "kz"
            ? $"Жазылды: *{val:F1}* ммоль/л ({typeText})\nҚорытынды: *{status}*\n{advice}"
            : $"Записано: *{val:F1}* ммоль/л ({typeText})\nСтатус: *{status}*\n{advice}";

        await _bot.AnswerCallbackQuery(cb.Id);
        await _bot.EditMessageText(
            cb.Message.Chat.Id,
            cb.Message.MessageId,
            msg,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            cancellationToken: ct
        );

        await ShowMenuAsync(user, cb.Message.Chat.Id, ct);
    }

    // =========================================================
    // Типы inline-кнопок
    // =========================================================
    private InlineKeyboardMarkup BuildTypeKeyboard(UserData user)
    {
        bool ru = user.Language == "ru";

        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(ru ? "🕒 Натощак" : "🕒 Ашқарын", "GLU_TYPE:fasting"),
                InlineKeyboardButton.WithCallbackData(ru ? "🍽 После еды" : "🍽 Тамақтан соң", "GLU_TYPE:after")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(ru ? "⏱ По времени" : "⏱ Уақыт бойынша", "GLU_TYPE:time")
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(ru ? "❌ Без типа" : "❌ Түрсіз", "GLU_TYPE:skip")
            }
        });
    }

    // =========================================================
    // Интерпретация уровня глюкозы
    // =========================================================
    private string InterpretGlucose(double v, string type, string lang)
    {
        bool ru = lang == "ru";

        if (v < 3.9)
            return ru ? "Низкий уровень" : "Төмен деңгей";

        if (v <= 7.0)
            return ru ? "Норма" : "Қалыпты";

        if (v <= 11.0)
            return ru ? "Повышенный" : "Жоғарылаған";

        return ru ? "Очень высокий!" : "Өте жоғары!";
    }

    // =========================================================
    // Советы
    // =========================================================
    private string AdviceGlucose(double v, string type, string lang)
    {
        bool ru = lang == "ru";

        if (v < 3.9)
            return ru
                ? "⚠️ Уровень понижен. Желательно съесть быстрые углеводы."
                : "⚠️ Деңгей төмен. Жылдам көмірсу қолданған жөн.";

        if (v <= 7.0)
            return ru
                ? "✔ Уровень в норме."
                : "✔ Деңгей қалыпты.";

        if (v <= 11.0)
            return ru
                ? "⚠️ Немного повышено. Рекомендуется контроль через 2 часа."
                : "⚠️ Сәл жоғарылаған. 2 сағаттан кейін қайта тексеру ұсынылады.";

        return ru
            ? "❗ Очень высокий уровень! Следует принять меры или обратиться к врачу."
            : "❗ Өте жоғары! Тез арада шара қолдану керек.";

    }

    // =========================================================
    // История измерений
    // =========================================================
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
                    var t = x.Time.ToLocalTime();
                    return $"{t:dd.MM HH:mm} — {x.Value:0.0} ({x.Type})";
                })
        );

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }

    // =========================================================
    // Статистика
    // =========================================================
    private async Task SendStatsAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.Glucose.Count == 0)
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Статистика жоқ." : "Статистики нет.",
                cancellationToken: ct);
            return;
        }

        var arr = user.Glucose.Select(x => x.Value).ToArray();
        double avg = arr.Average();

        await _bot.SendMessage(chatId,
            (user.Language == "kz" ? "Орташа мән: " : "Среднее значение: ") +
            avg.ToString("0.0"),
            cancellationToken: ct);
    }
}
