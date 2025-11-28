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

    // ---------------------------------------------------------
    // Показать главное меню глюкозы
    // ---------------------------------------------------------
    public async Task ShowMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[GLU] ShowMenu");

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { "📋 История", "📊 Статистика" },
            new KeyboardButton[] { "➕ Добавить измерение" },
            new KeyboardButton[] { user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад" }
        })
        {
            ResizeKeyboard = true
        };

        await _bot.SendMessage(
            chatId,
            user.Language == "kz" ? "Глюкоза мәзірі:" : "Меню глюкозы:",
            replyMarkup: kb,
            cancellationToken: ct
        );
    }

    // ---------------------------------------------------------
    // Обработка текстовых сообщений в фазе Glucose
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
            user.Phase = BotPhase.Glucose_TypeSelect;
            await AskTypeAsync(user, chatId, ct);
            return;
        }

        await ShowMenuAsync(user, chatId, ct);
    }

    // ---------------------------------------------------------
    // Обработка callback'ов
    // ---------------------------------------------------------
    public async Task HandleCallbackAsync(UserData user, CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Data == null || cb.Message == null)
            return;

        long chatId = cb.Message.Chat.Id;
        string data = cb.Data;

        BotLogger.Info($"[GLU] Callback: {data}");

        if (!data.StartsWith("GLU_TYPE:"))
        {
            BotLogger.Warn($"[GLU] Unknown callback: {data}");
            return;
        }

        string type = data.Split(':')[1]; // fasting / after / time / cancel

        if (type == "cancel")
        {
            user.Phase = BotPhase.Glucose;
            await ShowMenuAsync(user, chatId, ct);
            return;
        }

        // сохраняем тип временно
        user.TempGlucoseType = type;

        // переходим к вводу значения
        user.Phase = BotPhase.Glucose_ValueInput;
        await AskValueAsync(user, chatId, ct);
    }

    // ---------------------------------------------------------
    // Выбор типа измерения
    // ---------------------------------------------------------
    private async Task AskTypeAsync(UserData user, long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(
            chatId,
            user.Language == "kz" ? "Өлшеу түрін таңдаңыз:" : "Выберите тип измерения:",
            replyMarkup: BuildTypeKeyboard(user),
            cancellationToken: ct
        );
    }

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
                InlineKeyboardButton.WithCallbackData(ru ? "❌ Отмена" : "❌ Болдырмау", "GLU_TYPE:cancel")
            }
        });
    }

    // ---------------------------------------------------------
    // Ввод значения
    // ---------------------------------------------------------
    public async Task AskValueAsync(UserData user, long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(
            chatId,
            user.Language == "kz" ? "Мәнді енгізіңіз:" : "Введите значение:",
            cancellationToken: ct
        );
    }

    public async Task HandleValueInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        var normalized = text.Replace(',', '.');

        if (!double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            await _bot.SendMessage(
                chatId,
                user.Language == "kz" ? "Сан енгізіңіз!" : "Введите число!",
                cancellationToken: ct
            );
            return;
        }

        // Сохраняем
        user.Glucose.Add(new GlucoseRecord
        {
            Value = value,
            Type = user.TempGlucoseType ?? "",
            Time = DateTime.UtcNow
        });

        // очищаем временный тип
        user.TempGlucoseType = null;

        user.Phase = BotPhase.Glucose;

        await _bot.SendMessage(
            chatId,
            user.Language == "kz" ? "Сақталды!" : "Сохранено!",
            cancellationToken: ct
        );

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
                cancellationToken: ct
            );
            return;
        }

        string msg = string.Join("\n",
            user.Glucose
                .OrderByDescending(x => x.Time)
                .Take(10)
                .Select(x =>
                {
                    var t = x.Time.ToLocalTime();
                    string type = x.Type switch
                    {
                        "fasting" => " (натощак)",
                        "after"   => " (после еды)",
                        "time"    => " (по времени)",
                        _ => ""
                    };
                    return $"{t:dd.MM HH:mm} — {x.Value:0.0}{type}";
                })
        );

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
                cancellationToken: ct
            );
            return;
        }

        var arr = user.Glucose.Select(x => x.Value).ToArray();
        double avg = arr.Average();

        await _bot.SendMessage(
            chatId,
            (user.Language == "kz" ? "Орташа мән: " : "Среднее значение: ") + avg.ToString("0.0"),
            cancellationToken: ct
        );
    }
}
