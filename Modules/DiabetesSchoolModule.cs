using System.Text.Json;
using System.IO; // важно!
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Utils;
using DiabetesBot.Models;

namespace DiabetesBot.Modules;

public class DiabetesSchoolModule
{
    private readonly ITelegramBotClient _bot;

    private Dictionary<string, Dictionary<string, string>> _lessonsRu = new();
    private Dictionary<string, Dictionary<string, string>> _lessonsKk = new();

    public DiabetesSchoolModule(ITelegramBotClient bot)
    {
        _bot = bot;
        BotLogger.Info("[DS] Инициализация школы диабета");
        LoadLessonTexts();
    }

    // ============================================================
    // Загрузка JSON
    // ============================================================
    private void LoadLessonTexts()
    {
        try
        {
            string ruPath = Path.Combine(AppContext.BaseDirectory, "Data", "lang_ru.json");
            string kkPath = Path.Combine(AppContext.BaseDirectory, "Data", "lang_kk.json");

            BotLogger.Info($"[DS] RU JSON → {ruPath}");
            BotLogger.Info($"[DS] KK JSON → {kkPath}");

            // -------- RU --------
            if (System.IO.File.Exists(ruPath))
            {
                var jsonRaw = System.IO.File.ReadAllText(ruPath);
                var json = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonRaw);

                if (json != null && json.ContainsKey("ds.lessons"))
                {
                    _lessonsRu = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                        json["ds.lessons"].ToString()!
                    )!;
                }

                BotLogger.Info($"[DS] RU lessons loaded: {_lessonsRu.Count}");
            }
            else BotLogger.Warn("[DS] RU lessons NOT FOUND!");

            // -------- KZ --------
            if (System.IO.File.Exists(kkPath))
            {
                var jsonRaw = System.IO.File.ReadAllText(kkPath);
                var json = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonRaw);

                if (json != null && json.ContainsKey("ds.lessons"))
                {
                    _lessonsKk = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                        json["ds.lessons"].ToString()!
                    )!;
                }

                BotLogger.Info($"[DS] KZ lessons loaded: {_lessonsKk.Count}");
            }
            else BotLogger.Warn("[DS] KZ lessons NOT FOUND!");
        }
        catch (Exception ex)
        {
            BotLogger.Error("[DS] Ошибка загрузки уроков", ex);
        }
    }

    // ============================================================
    // Главное меню
    // ============================================================
    public async Task ShowMainMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        BotLogger.Info("[DS] MainMenu");

        string t1 = user.Language == "kz" ? "📘 1-сабақ: Жалпы ақпарат" : "📘 Урок 1: Общая информация";
        string t2 = user.Language == "kz" ? "📗 2-сабақ: Асқынулар" : "📗 Урок 2: Осложнения";
        string t3 = user.Language == "kz" ? "📙 3-сабақ: Өзін-өзі бақылау" : "📙 Урок 3: Самоконтроль";
        string t4 = user.Language == "kz" ? "📕 4-сабақ: Инсулин" : "📕 Урок 4: Инсулин";

        string back = user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { t1, t2 },
            new KeyboardButton[] { t3, t4 },
            new KeyboardButton[] { back }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(
            chatId,
            user.Language == "kz" ? "📚 Диабет мектебі" : "📚 Школа диабета",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ============================================================
    // Обработка текста → выбор главы
    // ============================================================
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text.StartsWith("📘")) { await ShowChapterAsync(user, chatId, 1, ct); return; }
        if (text.StartsWith("📗")) { await ShowChapterAsync(user, chatId, 2, ct); return; }
        if (text.StartsWith("📙")) { await ShowChapterAsync(user, chatId, 3, ct); return; }
        if (text.StartsWith("📕")) { await ShowChapterAsync(user, chatId, 4, ct); return; }

        await ShowMainMenuAsync(user, chatId, ct);
    }

    // ============================================================
    // Список уроков главы
    // ============================================================
    public async Task ShowChapterAsync(UserData user, long chatId, int chapter, CancellationToken ct)
    {
        var src = user.Language == "kz" ? _lessonsKk : _lessonsRu;

        if (!src.ContainsKey(chapter.ToString()))
        {
            await _bot.SendMessage(chatId, "Глава отсутствует.", cancellationToken: ct);
            return;
        }

        var lessons = src[chapter.ToString()];

        var kb = lessons.Keys
            .OrderBy(k => k)
            .Select(id => new[] { InlineKeyboardButton.WithCallbackData(id, $"DS_LESSON|{id}") })
            .ToList();

        kb.Add(new[]
        {
            InlineKeyboardButton.WithCallbackData(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад", "DS_BACK")
        });

        await _bot.SendMessage(chatId, $"Глава {chapter}",
            replyMarkup: new InlineKeyboardMarkup(kb),
            cancellationToken: ct);
    }

    // ============================================================
    // Конкретный урок
    // ============================================================
    public async Task ShowLessonAsync(UserData user, long chatId, string id, CancellationToken ct)
    {
        var src = user.Language == "kz" ? _lessonsKk : _lessonsRu;

        string chapter = id.Split('.')[0];

        if (!src.ContainsKey(chapter))
        {
            await _bot.SendMessage(chatId, "Глава отсутствует.", cancellationToken: ct);
            return;
        }

        if (!src[chapter].ContainsKey(id))
        {
            await _bot.SendMessage(chatId, $"{id} нет в базе.", cancellationToken: ct);
            return;
        }

        await _bot.SendMessage(chatId, src[chapter][id], cancellationToken: ct);
    }

    // ============================================================
    // CALLBACK
    // ============================================================
    public async Task HandleCallbackAsync(UserData user, CallbackQuery q, CancellationToken ct)
    {
        string data = q.Data!;
        long chatId = q.Message!.Chat.Id;

        if (data.StartsWith("DS_LESSON|"))
        {
            await ShowLessonAsync(user, chatId, data.Replace("DS_LESSON|", ""), ct);
            return;
        }

        if (data == "DS_BACK")
        {
            await ShowMainMenuAsync(user, chatId, ct);
            return;
        }
    }
}
