using System.Globalization;
using System.Linq;
using DiabetesBot.Models;
using DiabetesBot.Utils;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using Telegram.Bot.Types;

namespace DiabetesBot.Modules;

public class GlucoseModule
{
    private readonly ITelegramBotClient _bot;

    public GlucoseModule(ITelegramBotClient bot)
    {
        _bot = bot;
    }

    // ---------------------------------------------------------
    // Главное меню Глюкозы
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

        await _bot.SendMessage(chatId,
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
    // CALLBACKS
    // ---------------------------------------------------------
    public async Task HandleCallbackAsync(UserData user, CallbackQuery cb, CancellationToken ct)
    {
        if (cb.Data == null) return;

        BotLogger.Info($"[GLU] Callback: {cb.Data}");

        if (!cb.Data.StartsWith("GLU_TYPE:"))
            return;

        string type = cb.Data.Split(':')[1];

        if (type == "cancel")
        {
            user.Phase = BotPhase.Glucose;
            await ShowMenuAsync(user, cb.Message.Chat.Id, ct);
            return;
        }

        if (user.TempGlucoseValue == null)
        {
            await _bot.SendMessage(cb.Message.Chat.Id,
                "Ошибка: нет временного значения.",
                cancellationToken: ct);
            user.Phase = BotPhase.Glucose;
            return;
        }

        // Сохраняем запись
        user.Glucose.Add(new GlucoseRecord
        {
            Value = user.TempGlucoseValue.Value,
            Type = type,
            Time = DateTime.UtcNow
        });

        user.TempGlucoseValue = null;
        user.Phase = BotPhase.Glucose;

        await _bot.AnswerCallback(cb.Id);
        await _bot.SendMessage(cb.Message.Chat.Id, "Сохранено!", cancellationToken: ct);

        await ShowMenuAsync(user, cb.Message.Chat.Id, ct);
    }

    // ---------------------------------------------------------
    // Ввод значения
    // ---------------------------------------------------------
    public async Task AskValueAsync(UserData user, long chatId, CancellationToken ct)
    {
        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Мәнді енгізіңіз:" : "Введите значение:",
            cancellationToken: ct);
    }

    public async Task HandleValueInputAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        // Нормализуем точку/запятую
        var normalized = text.Replace(',', '.');

        if (!double.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value))
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Сан енгізіңіз!" : "Введите число!",
                cancellationToken: ct);
            return;
        }

        // Временно сохраняем значение до выбора типа
        user.TempGlucoseValue = value;

        // Меняем фазу — ВАЖНО!!!
        user.Phase = BotPhase.Glucose_ValueInputType;

        // Показываем inline-кнопки выбора типа
        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Өлшеу түрін таңдаңыз:" : "Выберите тип измерения:",
            replyMarkup: BuildTypeKeyboard(user),
            cancellationToken: ct);
    }


    // ---------------------------------------------------------
    // Inline клавиатура типа измерения
    // ---------------------------------------------------------
    private InlineKeyboardMarkup BuildTypeKeyboard(UserData user)
    {
        bool ru = user.Language == "ru";

        return new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(ru ? "🕒 Натощак"   : "🕒 Ашқарын",       "GLU_TYPE:fasting"),
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
    // История
    // ---------------------------------------------------------
    private async Task SendHistoryAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.Glucose.Count == 0)
        {
            await _bot.SendMessage(chatId, user.Language == "kz" ? "Өлшеулер жоқ." : "Нет измерений.", cancellationToken: ct);
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
            await _bot.SendMessage(chatId, user.Language == "kz" ? "Статистика жоқ." : "Статистики нет.", cancellationToken: ct);
            return;
        }

        var arr = user.Glucose.Select(x => x.Value).ToArray();
        double avg = arr.Average();

        await _bot.SendMessage(chatId,
            (user.Language == "kz" ? "Орташа мән: " : "Среднее значение: ") + avg.ToString("0.0"),
            cancellationToken: ct);
    }
}

