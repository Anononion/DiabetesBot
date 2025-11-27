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
    private Dictionary<string, Lesson> _lessons = new();

    public DiabetesSchoolModule(ITelegramBotClient bot)
    {
        _bot = bot;
        Load();
    }

    private void Load()
    {
        string ru = Path.Combine("Data", "lang_ru.json");
        string kk = Path.Combine("Data", "lang_kk.json");

        var r = JsonConvert.DeserializeObject<Dictionary<string, Lesson>>(File.ReadAllText(ru));
        var k = JsonConvert.DeserializeObject<Dictionary<string, Lesson>>(File.ReadAllText(kk));

        _lessons = r;
    }

    // ---------------------------------------------------------
    public async Task ShowMainMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var ik = new InlineKeyboardMarkup(
            _lessons.Keys.Select(id =>
                InlineKeyboardButton.WithCallbackData(_lessons[id].Title, $"school_open:{id}")
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
            await _bot.SendMessage(chatId, "Ошибка.", cancellationToken: ct);
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
        string content = lesson.Pages[user.LessonPage];

        var buttons = new List<InlineKeyboardButton[]>();

        if (user.LessonPage > 0)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("⬅️", "school_prev") });

        if (user.LessonPage < lesson.Pages.Count - 1)
            buttons.Add(new[] { InlineKeyboardButton.WithCallbackData("➡️", "school_next") });

        var ik = new InlineKeyboardMarkup(buttons);

        await _bot.SendMessage(chatId,
            $"<b>{lesson.Title}</b>\n\n{content}",
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Html,
            replyMarkup: ik,
            cancellationToken: ct);
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
