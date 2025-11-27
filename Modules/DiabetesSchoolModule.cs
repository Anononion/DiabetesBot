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

    // Уроки после разбора JSON:
    // key: "1" / "2" / "3" / "4"
    // value: Lesson { Title, Pages }
    private Dictionary<string, Lesson> _lessons = new();

    public DiabetesSchoolModule(ITelegramBotClient bot)
    {
        _bot = bot;
        LoadLessons();
    }

    // ---------------------------------------------------------
    // Загрузка уроков из lang_ru.json / lang_kk.json
    // ---------------------------------------------------------
    private void LoadLessons()
    {
        string ru = Path.Combine("Data", "lang_ru.json");

        var json = JsonConvert.DeserializeObject<Dictionary<string, object>>(
            File.ReadAllText(ru)
        );

        // Берем блок "ds.lessons"
        var lessonsRaw = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(
            json["ds.lessons"].ToString()
        );

        foreach (var lessonGroup in lessonsRaw)
        {
            string lessonId = lessonGroup.Key;

            // lessonGroup.Value = словарь: "1.1": "...", "1.2": "..."
            var pages = lessonGroup.Value
                .OrderBy(p => p.Key)     // сортируем страницы
                .Select(p => p.Value)    // берём текст
                .ToList();

            string title = pages[0].Split('\n').First().Trim(); // первая строка первой страницы — заголовок

            _lessons[lessonId] = new Lesson
            {
                Id = lessonId,
                Title = title,
                Pages = pages
            };
        }

        BotLogger.Info($"[DS] Lessons loaded: {_lessons.Count}");
    }

    // ---------------------------------------------------------
    public async Task ShowMainMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var ik = new InlineKeyboardMarkup(
            _lessons.Select(l =>
                InlineKeyboardButton.WithCallbackData(l.Value.Title, $"school_open:{l.Key}")
            )
        );

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Диабет мектебі:" : "Школа диабета:",
            replyMarkup: ik,
            cancellationToken: ct);
    }

    // ---------------------------------------------------------
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text.Contains("Назад") || text.Contains("Артқа"))
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        await ShowMainMenuAsync(user, chatId, ct);
    }

    // ---------------------------------------------------------
    public async Task OpenLessonAsync(UserData user, long chatId, string lessonId, CancellationToken ct)
    {
        if (!_lessons.ContainsKey(lessonId))
        {
            await _bot.SendMessage(chatId, "Ошибка: урок не найден.", cancellationToken: ct);
            return;
        }

        user.CurrentLesson = lessonId;
        user.LessonPage = 0;
        user.Phase = BotPhase.DiabetesSchool;

        await ShowLessonPageAsync(user, chatId, ct);
    }

    // ---------------------------------------------------------
    public async Task ShowLessonPageAsync(UserData user, long chatId, CancellationToken ct)
    {
        var lesson = _lessons[user.CurrentLesson];

        int page = user.LessonPage;
        if (page < 0) page = 0;
        if (page >= lesson.Pages.Count) page = lesson.Pages.Count - 1;

        string content = lesson.Pages[page];

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

    // ---------------------------------------------------------
    public async Task PrevPageAsync(UserData user, long chatId, CancellationToken ct)
    {
        if (user.LessonPage > 0)
            user.LessonPage--;

        await ShowLessonPageAsync(user, chatId, ct);
    }

    public async Task NextPageAsync(UserData user, long chatId, CancellationToken ct)
    {
        var count = _lessons[user.CurrentLesson].Pages.Count;

        if (user.LessonPage < count - 1)
            user.LessonPage++;

        await ShowLessonPageAsync(user, chatId, ct);
    }
}
