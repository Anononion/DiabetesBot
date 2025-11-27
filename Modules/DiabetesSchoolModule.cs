using DiabetesBot.Models;
using DiabetesBot.Utils;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace DiabetesBot.Modules;

public class DiabetesSchoolModule
{
    private readonly ITelegramBotClient _bot;

    // Два набора уроков
    private Dictionary<string, Lesson> _lessonsRu = new();
    private Dictionary<string, Lesson> _lessonsKk = new();

    public DiabetesSchoolModule(ITelegramBotClient bot)
    {
        _bot = bot;

        LoadLessons("ru");
        LoadLessons("kk");
    }

    // ============================================================
    // ЗАГРУЗКА УРОКОВ
    // ============================================================
    private void LoadLessons(string lang)
    {
        string file = Path.Combine("Data", $"lang_{lang}.json");

        if (!File.Exists(file))
        {
            BotLogger.Warn($"[DS] file missing: {file}");
            return;
        }

        var root = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            File.ReadAllText(file)
        );

        if (!root.ContainsKey("ds.lessons"))
        {
            BotLogger.Warn($"[DS] no ds.lessons in {file}");
            return;
        }

        // структура: "ds.lessons" → { "1": { "1.1": "...", "1.2": "..." }, ... }
        var lessonsRaw = JsonConvert.DeserializeObject<
            Dictionary<string, Dictionary<string, string>>
        >(root["ds.lessons"].ToString());

        var dict = new Dictionary<string, Lesson>();

        foreach (var group in lessonsRaw)
        {
            string lessonId = group.Key;

            // сортируем страницы по ключу (1.1, 1.2…)
            var pages = group.Value
                .OrderBy(p => p.Key)
                .Select(p => p.Value)
                .ToList();

            string title = ExtractTitle(pages[0]);

            dict[lessonId] = new Lesson
            {
                Id = lessonId,
                Title = title,
                Pages = pages
            };
        }

        if (lang == "ru")
            _lessonsRu = dict;
        else
            _lessonsKk = dict;

        BotLogger.Info($"[DS] Lessons loaded ({lang}): {dict.Count}");
    }

    private string ExtractTitle(string page)
    {
        // первая строка первой страницы — заголовок
        var firstLine = page.Split('\n').First().Trim();
        return firstLine.Length > 0 ? firstLine : "Урок";
    }

    // ============================================================
    // ВЫБОР НАБОРА УРОКОВ ПО ЯЗЫКУ ПОЛЬЗОВАТЕЛЯ
    // ============================================================
    private Dictionary<string, Lesson> GetLessons(UserData user)
    {
        return user.Language == "kz" ? _lessonsKk : _lessonsRu;
    }

    // ============================================================
    // ГЛАВНОЕ МЕНЮ ШКОЛЫ
    // ============================================================
    public async Task ShowMainMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var lessons = GetLessons(user);

        var ik = new InlineKeyboardMarkup(
            lessons.Select(l =>
                InlineKeyboardButton.WithCallbackData(l.Value.Title, $"school_open:{l.Key}")
            )
        );

        string title = user.Language == "kz" ? "Диабет мектебі:" : "Школа диабета:";

        await _bot.SendMessage(chatId, title, replyMarkup: ik, cancellationToken: ct);
    }

    // ============================================================
    // ТЕКСТОВЫЕ КНОПКИ (Назад)
    // ============================================================
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text.Contains("Назад") || text.Contains("Артқа"))
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        await ShowMainMenuAsync(user, chatId, ct);
    }

    // ============================================================
    // ОТКРЫТИЕ УРОКА
    // ============================================================
    public async Task OpenLessonAsync(UserData user, long chatId, string lessonId, CancellationToken ct)
    {
        var lessons = GetLessons(user);

        if (!lessons.ContainsKey(lessonId))
        {
            await _bot.SendMessage(chatId, "Ошибка: урок не найден.", cancellationToken: ct);
            return;
        }

        user.CurrentLesson = lessonId;
        user.LessonPage = 0;
        user.Phase = BotPhase.DiabetesSchool;

        await ShowLessonPageAsync(user, chatId, ct);
    }

    // ============================================================
    // ПОКАЗ СТРАНИЦЫ
    // ============================================================
    public async Task ShowLessonPageAsync(UserData user, long chatId, CancellationToken ct)
    {
        var lessons = GetLessons(user);
        var lesson = lessons[user.CurrentLesson];

        int page = Math.Clamp(user.LessonPage, 0, lesson.Pages.Count - 1);
        string content = lesson.Pages[page];

        // кнопки навигации
        var buttons = new List<InlineKeyboardButton[]>();

        if (page > 0)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️", "school_prev") });

        if (page < lesson.Pages.Count - 1)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➡️", "school_next") });

        await _bot.SendMessage(
            chatId,
            $"<b>{lesson.Title}</b>\n\n{content}",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: new InlineKeyboardMarkup(buttons),
            cancellationToken: ct
        );
    }

    // ============================================================
    // СТРАНИЦА НАЗАД
    // ============================================================
    public async Task PrevPageAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.LessonPage > 0)
            user.LessonPage--;

        await ShowLessonPageAsync(user, chatId, ct);
    }

    // ============================================================
    // СТРАНИЦА ВПЕРЕД
    // ============================================================
    public async Task NextPageAsync(UserData user, long chatId, CancellationToken ct)
    {
        var lessons = GetLessons(user);
        var count = lessons[user.CurrentLesson].Pages.Count;

        if (user.LessonPage < count - 1)
            user.LessonPage++;

        await ShowLessonPageAsync(user, chatId, ct);
    }
}
