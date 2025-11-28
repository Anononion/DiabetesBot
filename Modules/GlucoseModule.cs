using System.Globalization;
using System.Linq;
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

    // ---------------------------------------------------------
    // Главное меню глюкозы
    // ---------------------------------------------------------
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[GLU] ShowMenu");

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton("📋 История"), new KeyboardButton("📊 Статистика") },
            new[] { new KeyboardButton("➕ Добавить измерение") },
            new[] { new KeyboardButton(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад") }
        })
        {
            ResizeKeyboard = true
        };

        await _bot.SendMessage(
            chatId,
            user.Language == "kz" ? "Глюкоза мәзірі:" : "Меню глюкозы:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ---------------------------------------------------------
    // Обработка текстовых команд (фаза Glucose)
    // ---------------------------------------------------------
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

    // ---------------------------------------------------------
    // Ввод значения (фаза Glucose_ValueInput)
    // ---------------------------------------------------------
    public async Task AskValueAsync(UserData user, long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(
            chatId,
            user.Language == "kz" ? "Мәнді енгізіңіз:" : "Введите значение:",
            cancellationToken: ct);
    }

    public async Task HandleValueInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        // локальный "назад"
        if (text.Contains("Назад") || text.Contains("Артқа"))
        {
            user.Phase = BotPhase.Glucose;
            user.PendingGlucoseValue = null;

            await ShowMenuAsync(user, chatId, ct);
            return;
        }

        var normalized = text.Replace(',', '.');

        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            await _bot.SendMessage(
                chatId,
                user.Language == "kz" ? "Сан енгізіңіз!" : "Введите число!",
                cancellationToken: ct);
            return;
        }

        user.PendingGlucoseValue = value;
        user.Phase = BotPhase.Glucose_ValueInputType;

        await AskTypeAsync(user, chatId, ct);
    }

    // ---------------------------------------------------------
    // Выбор типа (фаза Glucose_ValueInputType) — обычная клавиатура
    // ---------------------------------------------------------
    public async Task AskTypeAsync(UserData user, long chatId, CancellationToken ct)
    {
        bool ru = user.Language == "ru";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new[]
            {
                new KeyboardButton(ru ? "🕒 Натощак"    : "🕒 Ашқарын"),
                new KeyboardButton(ru ? "🍽 После еды" : "🍽 Тамақтан соң")
            },
            new[]
            {
                new KeyboardButton(ru ? "⏱ По времени" : "⏱ Уақыт бойынша")
            },
            new[]
            {
                new KeyboardButton(ru ? "❌ Отмена" : "❌ Болдырмау")
            }
        })
        {
            ResizeKeyboard = true
        };

        await _bot.SendMessage(
            chatId,
            ru ? "Выберите тип измерения:" : "Өлшеу түрін таңдаңыз:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    public async Task HandleTypeTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        bool ru = user.Language == "ru";

        if (text.Contains("Отмена") || text.Contains("Болдырмау"))
        {
            user.PendingGlucoseValue = null;
            user.Phase = BotPhase.Glucose;

            await _bot.SendMessage(
                chatId,
                ru ? "Отменено." : "Болдырылды.",
                cancellationToken: ct);

            await ShowMenuAsync(user, chatId, ct);
            return;
        }

        if (user.PendingGlucoseValue == null)
        {
            // что-то пошло не так — просто выходим в меню
            user.Phase = BotPhase.Glucose;
            await ShowMenuAsync(user, chatId, ct);
            return;
        }

        string typeCode;
        if (text.Contains("Натощак") || text.Contains("Ашқарын"))
            typeCode = "fasting";
        else if (text.Contains("После еды") || text.Contains("Тамақтан соң"))
            typeCode = "after";
        else if (text.Contains("По времени") || text.Contains("Уақыт бойынша"))
            typeCode = "time";
        else
        {
            await _bot.SendMessage(
                chatId,
                ru
                    ? "Пожалуйста, выберите вариант с клавиатуры."
                    : "Пернетақтадағы нұсқалардың бірін таңдаңыз.",
                cancellationToken: ct);

            await AskTypeAsync(user, chatId, ct);
            return;
        }

        user.Glucose.Add(new GlucoseRecord
        {
            Value = user.PendingGlucoseValue.Value,
            Type  = typeCode,
            Time  = DateTime.UtcNow
        });

        user.PendingGlucoseValue = null;
        user.Phase = BotPhase.Glucose;

        await _bot.SendMessage(
            chatId,
            ru ? "Сохранено!" : "Сақталды!",
            cancellationToken: ct);

        await ShowMenuAsync(user, chatId, ct);
    }

    // ---------------------------------------------------------
    // История
    // ---------------------------------------------------------
    private async Task SendHistoryAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.Glucose.Count == 0)
        {
            await _bot.SendMessage(
                chatId,
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
                    var typePart = string.IsNullOrWhiteSpace(x.Type) ? "" : $" ({x.Type})";
                    return $"{t:dd.MM HH:mm} — {x.Value:0.0}{typePart}";
                }));

        await _bot.SendMessage(chatId, msg, cancellationToken: ct);
    }

    // ---------------------------------------------------------
    // Статистика
    // ---------------------------------------------------------
    private async Task SendStatsAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.Glucose.Count == 0)
        {
            await _bot.SendMessage(
                chatId,
                user.Language == "kz" ? "Статистика жоқ." : "Статистики нет.",
                cancellationToken: ct);
            return;
        }

        var arr = user.Glucose.Select(x => x.Value).ToArray();
        double avg = arr.Average();

        await _bot.SendMessage(
            chatId,
            (user.Language == "kz" ? "Орташа мән: " : "Среднее значение: ") + avg.ToString("0.0"),
            cancellationToken: ct);
    }
}

