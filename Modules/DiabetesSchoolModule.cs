using System.Text;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Models;
using DiabetesBot.Utils;
using DiabetesBot.Services;

namespace DiabetesBot.Modules;

public class DiabetesSchoolModule
{
    private readonly ITelegramBotClient _bot;

    // lessons["1"]["1.1"] = "text"
    private Dictionary<string, Dictionary<string, string>> _lessonsRu = new();
    private Dictionary<string, Dictionary<string, string>> _lessonsKk = new();

    public DiabetesSchoolModule(ITelegramBotClient bot)
    {
        _bot = bot;
        LoadLessons();
    }

    private void LoadLessons()
    {
        string ruPath = Path.Combine("Data", "lang_ru.json");
        string kkPath = Path.Combine("Data", "lang_kk.json");

        var ruJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(ruPath));
        var kkJson = JsonConvert.DeserializeObject<Dictionary<string, object>>(File.ReadAllText(kkPath));

        _lessonsRu = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(ruJson["ds.lessons"].ToString()!)!;
        _lessonsKk = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(kkJson["ds.lessons"].ToString()!)!;
    }

    private Dictionary<string, Dictionary<string, string>> GetLessons(string lang)
        => lang == "kz" ? _lessonsKk : _lessonsRu;

    // ============================
    // MAIN MENU OF SCHOOL
    // ============================
    public async Task ShowMainMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var lessons = GetLessons(user.Language);

        var menu = lessons.Keys
            .OrderBy(k => int.Parse(k))
            .Select(k => new[] { $"📘 Урок {k}" })
            .ToList();

        menu.Add(new[] { user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад" });

        await _bot.SendMessage(
            chatId,
            user.Language == "kz" ? "Диабет мектебі:" : "Школа диабета:",
            replyMarkup: new ReplyKeyboardMarkup(menu) { ResizeKeyboard = true },
            cancellationToken: ct);
    }

    // ============================
    // HANDLE TEXT (BUTTONS)
    // ============================
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text == "⬅️ Назад" || text == "⬅️ Артқа")
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        // Example: "📘 Урок 2"
        if (text.StartsWith("📘"))
        {
            string num = text.Replace("📘 Урок", "").Trim();
            if (int.TryParse(num, out int lessonNumber))
            {
                await OpenLessonAsync(user, chatId, lessonNumber, ct);
            }
            return;
        }
    }

    // ============================
    // OPEN LESSON (PAGE 1.1)
    // ============================
    public async Task OpenLessonAsync(UserData user, long chatId, int lesson, CancellationToken ct)
    {
        user.CurrentLesson = lesson;
        user.LessonPage = 0;

        await ShowLessonPageAsync(user, chatId, ct);
    }

    // ============================
    // SHOW LESSON PAGE (1.1 → 1.2 → ...)
    // ============================
    public async Task ShowLessonPageAsync(UserData user, long chatId, CancellationToken ct)
    {
        var lessons = GetLessons(user.Language);

        string lessonKey = user.CurrentLesson.ToString();

        if (!lessons.ContainsKey(lessonKey))
        {
            await _bot.SendMessage(chatId, "Ошибка: урок не найден.", cancellationToken: ct);
            return;
        }

        var pages = lessons[lessonKey]
            .OrderBy(k => double.Parse(k.Key.Replace($"{lessonKey}.", "")))
            .ToList();

        if (user.LessonPage < 0) user.LessonPage = 0;
        if (user.LessonPage >= pages.Count) user.LessonPage = pages.Count - 1;

        string pageText = pages[user.LessonPage].Value;

        var buttons = new List<KeyboardButton[]>();

        if (user.LessonPage > 0)
            buttons.Add(new[] { new KeyboardButton(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад") });

        if (user.LessonPage < pages.Count - 1)
            buttons.Add(new[] { new KeyboardButton(user.Language == "kz" ? "➡️ Келесі" : "➡️ Далее") });

        buttons.Add(new[]
        {
            new KeyboardButton(user.Language == "kz" ? "📚 Мәзірге оралу" : "📚 В меню школы")
        });

        await _bot.SendMessage(
            chatId,
            pageText,
            replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true },
            cancellationToken: ct);
    }

    // ============================
    // OFFSET PAGE (NEXT/PREV)
    // ============================
    public async Task OffsetPageAsync(UserData user, long chatId, int delta, CancellationToken ct)
    {
        user.LessonPage += delta;
        await ShowLessonPageAsync(user, chatId, ct);
    }
}
