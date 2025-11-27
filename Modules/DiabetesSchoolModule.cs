using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Services;
using DiabetesBot.Utils;

namespace DiabetesBot.Modules;

public class DiabetesSchoolModule
{
    private readonly TelegramBotClient _bot;
    private readonly UserStateService _state;
    private readonly JsonStorageService _storage;

    private Dictionary<string, Dictionary<string, string>> _lessonsRu =
        new Dictionary<string, Dictionary<string, string>>();

    private Dictionary<string, Dictionary<string, string>> _lessonsKk =
        new Dictionary<string, Dictionary<string, string>>();

    public DiabetesSchoolModule(
        TelegramBotClient bot,
        UserStateService state,
        JsonStorageService storage)
    {
        _bot = bot;
        _state = state;
        _storage = storage;

        BotLogger.Info("[DS] Конструктор: запускаем загрузку уроков");

        LoadLessonTexts();
    }

    // ============================================================
    // ЗАГРУЗКА ТЕКСТОВ УРОКОВ ИЗ JSON
    // ============================================================
    private async void LoadLessonTexts()
    {
        BotLogger.Info("[DS] Загружаем тексты уроков...");

        try
        {
            string dataDir = Path.Combine(
                AppContext.BaseDirectory,
                "Data", "users"
            );

            string ruPath = Path.Combine(dataDir, "lang_ru.json");
            string kkPath = Path.Combine(dataDir, "lang_kk.json");

            BotLogger.Info($"[DS] RU path: {ruPath}, Exists={System.IO.File.Exists(ruPath)}");
            BotLogger.Info($"[DS] KK path: {kkPath}, Exists={System.IO.File.Exists(kkPath)}");

            if (System.IO.File.Exists(ruPath))
            {
                var json = JsonSerializer.Deserialize<Dictionary<string, object>>(System.IO.File.ReadAllText(ruPath));
                if (json != null && json.ContainsKey("ds.lessons"))
                {
                    _lessonsRu =
                        JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                            json["ds.lessons"].ToString()!
                        )!;
                    BotLogger.Info("[DS] Загружены уроки RU");
                }
            }

            if (System.IO.File.Exists(kkPath))
            {
                var json = JsonSerializer.Deserialize<Dictionary<string, object>>(System.IO.File.ReadAllText(kkPath));
                if (json != null && json.ContainsKey("ds.lessons"))
                {
                    _lessonsKk =
                        JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(
                            json["ds.lessons"].ToString()!
                        )!;
                    BotLogger.Info("[DS] Загружены уроки KK");
                }
            }

            BotLogger.Info($"[DS] Итог: RU глав={_lessonsRu.Count}, KK глав={_lessonsKk.Count}");
        }
        catch (Exception ex)
        {
            BotLogger.Error($"[DS] Ошибка загрузки уроков: {ex.Message}", ex);
        }
    }

    // ============================================================
    // Получение языка
    // ============================================================
    private async Task<string> GetLangAsync(long userId)
    {
        var user = await _storage.LoadAsync(userId);
        string lang = string.IsNullOrWhiteSpace(user.Language) ? "ru" : user.Language;
        BotLogger.Info($"[DS] GetLangAsync: userId={userId}, lang={lang}");
        return lang;
    }

    // ============================================================
    // Главное меню школы диабета
    // ============================================================
    public async Task ShowMainMenuAsync(long chatId, long userId, CancellationToken ct)
    {
        BotLogger.Info($"[DS] ShowMainMenuAsync: chatId={chatId}");

        var lang = await GetLangAsync(userId);

        string t1 = lang == "kk" ? "📘 1-сабақ: Жалпы ақпарат" : "📘 Урок 1: Общая информация";
        string t2 = lang == "kk" ? "📗 2-сабақ: Асқынулар" : "📗 Урок 2: Осложнения";
        string t3 = lang == "kk" ? "📙 3-сабақ: Өзін-өзі бақылау" : "📙 Урок 3: Самоконтроль";
        string t4 = lang == "kk" ? "📕 4-сабақ: Инсулин" : "📕 Урок 4: Инсулин";

        string back = lang == "kk" ? "⬅️ Мәзірге" : "⬅️ В меню";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new[] { new KeyboardButton(t1), new KeyboardButton(t2) },
            new[] { new KeyboardButton(t3), new KeyboardButton(t4) },
            new[] { new KeyboardButton(back) }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId, "📚 *Школа диабета*", replyMarkup: kb, cancellationToken: ct);
    }

    // ============================================================
    // Обработка текста
    // ============================================================
    public async Task HandleTextAsync(long userId, long chatId, string text, CancellationToken ct)
    {
        BotLogger.Info($"[DS] HandleTextAsync: '{text}'");

        if (text.StartsWith("📘")) { await ShowChapterMenuAsync(chatId, 1, ct); return; }
        if (text.StartsWith("📗")) { await ShowChapterMenuAsync(chatId, 2, ct); return; }
        if (text.StartsWith("📙")) { await ShowChapterMenuAsync(chatId, 3, ct); return; }
        if (text.StartsWith("📕")) { await ShowChapterMenuAsync(chatId, 4, ct); return; }

        BotLogger.Warn("[DS] HandleTextAsync: текст не распознан, показываем главное меню");
        await ShowMainMenuAsync(chatId, userId, ct);
    }

    // ============================================================
    // Меню уроков
    // ============================================================
    public async Task ShowChapterMenuAsync(long chatId, int chapter, CancellationToken ct)
    {
        BotLogger.Info($"[DS] ShowChapterMenuAsync: chapter={chapter}");

        int count = chapter switch
        {
            1 => 5,
            2 => 5,
            3 => 3,
            4 => 5,
            _ => 0
        };

        var kb = new List<InlineKeyboardButton[]>();

        for (int i = 1; i <= count; i++)
        {
            string id = $"{chapter}.{i}";
            kb.Add(new[] { InlineKeyboardButton.WithCallbackData($"Урок {id}", $"DS_LESSON|{id}") });
        }

        kb.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️ Назад", "DS_BACK") });

        await _bot.SendMessage(
            chatId,
            $"Выберите урок главы {chapter}:",
            replyMarkup: new InlineKeyboardMarkup(kb),
            cancellationToken: ct
        );
    }

    // ============================================================
    // Показ текста урока
    // ============================================================
    public async Task ShowLessonTextAsync(long chatId, long userId, string lessonId, CancellationToken ct)
    {
        BotLogger.Info($"[DS] ShowLessonTextAsync: lessonId={lessonId}");

        var lang = await GetLangAsync(userId);

        var src = lang == "kk" ? _lessonsKk : _lessonsRu;

        BotLogger.Info($"[DS] В текущем языке глав={src.Count}");

        string chapter = lessonId.Split('.')[0];

        BotLogger.Info($"[DS] Проверяем главу '{chapter}' → Contains={src.ContainsKey(chapter)}");

        if (src.ContainsKey(chapter))
        {
            BotLogger.Info($"[DS] В главе {chapter} уроков: {src[chapter].Count}");

            if (src[chapter].ContainsKey(lessonId))
            {
                BotLogger.Info($"[DS] Урок найден, отправляем текст");
                await _bot.SendMessage(chatId, src[chapter][lessonId], cancellationToken: ct);
                return;
            }
            else
            {
                BotLogger.Warn($"[DS] В главе {chapter} НЕТ урока '{lessonId}'");
            }
        }

        await _bot.SendMessage(chatId, $"Текст урока {lessonId} пока не добавлен.", cancellationToken: ct);
    }

    // ============================================================
    // CALLBACK
    // ============================================================
    public async Task HandleCallbackAsync(CallbackQuery query, CancellationToken ct)
    {
        string data = query.Data!;
        long chatId = query.Message!.Chat.Id;
        long userId = query.From.Id;

        BotLogger.Info($"[DS] HandleCallbackAsync: data='{data}'");

        if (data.StartsWith("DS_LESSON|"))
        {
            string lessonId = data.Replace("DS_LESSON|", "");
            await ShowLessonTextAsync(chatId, userId, lessonId, ct);
            return;
        }

        if (data == "DS_BACK")
        {
            await ShowMainMenuAsync(chatId, userId, ct);
            return;
        }

        BotLogger.Warn($"[DS] Неизвестный callback: {data}");
    }
}
