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

    // =============================================
    // Главное меню
    // =============================================
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var kb = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(user.Language == "kz" ? "➕ Өлшеу қосу" : "➕ Добавить измерение") },
            new[] {
                new KeyboardButton(user.Language == "kz" ? "📋 Тарих" : "📋 История"),
                new KeyboardButton(user.Language == "kz" ? "📊 Статистика" : "📊 Статистика")
            },
            new[] { new KeyboardButton(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад") }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(
            chatId,
            user.Language == "kz" ? "Глюкоза мәзірі:" : "Меню глюкозы:",
            replyMarkup: kb,
            cancellationToken: ct
        );
    }

    // =============================================
    // Обработка текстов при фазе BotPhase.Glucose
    // =============================================
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text.Contains("➕") || text.Contains("қосу"))
        {
            await AskTypeAsync(user, chatId, ct);
            return;
        }

        if (text.Contains("История") || text.Contains("Тарих"))
        {
            await ShowHistoryAsync(user, chatId, ct);
            return;
        }

        if (text.Contains("Статистика"))
        {
            await ShowStatsAsync(user, chatId, ct);
            return;
        }

        await ShowMenuAsync(user, chatId, ct);
    }

    // =============================================
    // Выбор типа измерения
    // =============================================
    private async Task AskTypeAsync(UserData user, long chatId, CancellationToken ct)
    {
        user.Phase = BotPhase.Glucose_ValueInputType;

        var kb = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    user.Language == "kz" ? "⏱ Ашқарынға" : "⏱ Натощак",
                    "GLU_TYPE:fasting"
                ),
                InlineKeyboardButton.WithCallbackData(
                    user.Language == "kz" ? "🍽 Тамақтан кейін" : "🍽 После еды",
                    "GLU_TYPE:after"
                )
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    user.Language == "kz" ? "⏰ Уақыт бойынша" : "⏰ По времени",
                    "GLU_TYPE:time"
                )
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(
                    user.Language == "kz" ? "❌ Болдырмау" : "❌ Отмена",
                    "GLU_TYPE:cancel"
                )
            }
        });

        await _bot.SendMessage(
            chatId,
            user.Language == "kz" ? "Өлшеу түрін таңдаңыз:" : "Выберите тип измерения:",
            replyMarkup: kb,
            cancellationToken: ct
        );
    }

    // =============================================
    // Обработка callback — выбор типа
    // =============================================
    public async Task HandleCallbackAsync(UserData user, CallbackQuery cb, CancellationToken ct)
    {
        if (!cb.Data!.StartsWith("GLU_TYPE:"))
            return;

        string type = cb.Data.Split(':')[1];
        long chatId = cb.Message!.Chat.Id;

        if (type == "cancel")
        {
            user.Phase = BotPhase.Glucose;
            await ShowMenuAsync(user, chatId, ct);
            return;
        }

        // сохраняем тип
        user.TempGlucoseType = type;

        user.Phase = BotPhase.Glucose_ValueInput;

        await _bot.SendMessage(
            chatId,
            user.Language == "kz"
                ? "Глюкоза мәнін енгізіңіз (мысалы: 5.6):"
                : "Введите значение глюкозы (например: 5.6):",
            cancellationToken: ct
        );
    }

    // =============================================
    // Ввод значения
    // =============================================
    public async Task HandleValueInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (!double.TryParse(text.Replace(",", "."), out double val))
        {
            await _bot.SendMessage(
                chatId,
                user.Language == "kz" ? "Сан енгізіңіз!" : "Введите число!",
                cancellationToken: ct
            );
            return;
        }

        string type = user.TempGlucoseType ?? "time";

        // сохраняем
        user.Glucose.Add(new GlucoseRecord
        {
            Time = DateTime.Now,
            Value = val,
            Type = type
        });

        // интерпретация
        string status = Interpret(val, type, user.Language);
        string advice = Advice(val, type, user.Language);

        await _bot.SendMessage(
            chatId,
            user.Language == "kz"
                ? $"Жазылды: {val:F1} ммоль/л ({type})\nҚорытынды: {status}\n{advice}"
                : $"Записано: {val:F1} ммоль/л ({type})\nСтатус: {status}\n{advice}",
            cancellationToken: ct
        );

        user.Phase = BotPhase.Glucose;
        await ShowMenuAsync(user, chatId, ct);
    }

    // =============================================
    // История
    // =============================================
    private async Task ShowHistoryAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.Glucose.Count == 0)
        {
            await _bot.SendMessage(
                chatId,
                user.Language == "kz" ? "Тарих бос." : "История пуста.",
                cancellationToken: ct
            );
            return;
        }

        var items = user.Glucose
            .OrderByDescending(x => x.Time)
            .Take(10)
            .Select(x => $"{x.Time:dd.MM HH:mm} — {x.Value:F1} ({x.Type})");

        await _bot.SendMessage(
            chatId,
            string.Join("\n", items),
            cancellationToken: ct
        );
    }

    // =============================================
    // Статистика (7 дней)
    // =============================================
    private async Task ShowStatsAsync(UserData user, long chatId, CancellationToken ct)
    {
        var now = DateTime.Now;

        var last7 = user.Glucose
            .Where(x => (now - x.Time).TotalDays <= 7)
            .ToList();

        if (last7.Count == 0)
        {
            await _bot.SendMessage(
                chatId,
                user.Language == "kz"
                    ? "Соңғы 7 күнде деректер жоқ."
                    : "Нет данных за последние 7 дней.",
                cancellationToken: ct
            );
            return;
        }

        double avg = last7.Average(x => x.Value);
        double min = last7.Min(x => x.Value);
        double max = last7.Max(x => x.Value);

        await _bot.SendMessage(
            chatId,
            user.Language == "kz"
                ? $"📊 7 күн статистикасы:\nОрташа: {avg:F1}\nМин: {min:F1}\nМакс: {max:F1}\nБарлығы: {last7.Count}"
                : $"📊 Статистика за 7 дней:\nСреднее: {avg:F1}\nМин: {min:F1}\nМакс: {max:F1}\nЗаписей: {last7.Count}",
            cancellationToken: ct
        );
    }

    // =============================================
    // Интерпретация значений
    // =============================================
    private string Interpret(double v, string type, string lang)
    {
        string low = lang == "kz" ? "🟡 Төмен" : "🟡 Понижено";
        string norm = lang == "kz" ? "🟢 Норма" : "🟢 Норма";
        string high = lang == "kz" ? "🟠 Жоғары" : "🟠 Повышено";
        string danger = lang == "kz" ? "🔴 Өте жоғары" : "🔴 Очень высокое";

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
            if (v <= 11) return high;
            return danger;
        }

        if (type == "time")
        {
            if (v < 3.9) return low;
            if (v <= 11.1) return norm;
            return danger;
        }

        return norm;
    }

    // =============================================
    // Советы
    // =============================================
    private string Advice(double v, string type, string lang)
    {
        if (v < 3.9)
            return lang == "kz"
                ? "Гипогликемия: тәтті шай ішіңіз."
                : "Гипогликемия: выпейте сладкий чай.";

        if (v >= 11.1)
            return lang == "kz"
                ? "Жоғары глюкоза: су ішіңіз, өлшеуді қайталаңыз."
                : "Высокая глюкоза: пейте воду и повторите измерение.";

        return lang == "kz"
            ? "Көрсеткіш қалыпты."
            : "Значение в норме.";
    }
}
