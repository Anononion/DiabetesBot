using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Models;
using DiabetesBot.Utils;

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

        _lessonsRu = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(
            ruJson["ds.lessons"].ToString()!)!;

        _lessonsKk = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(
            kkJson["ds.lessons"].ToString()!)!;
    }

    private Dictionary<string, Dictionary<string, string>> GetLessons(string lang)
        => lang == "kz" ? _lessonsKk : _lessonsRu;

    // ============================================================
    // MAIN MENU
    // ============================================================
    public async Task ShowMainMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var lessons = GetLessons(user.Language);

        var list = new List<KeyboardButton[]>();

        foreach (var lesson in lessons.Keys.OrderBy(k => int.Parse(k)))
        {
            string title = user.Language == "kz"
                ? $"📘 Сабақ {lesson}"
                : $"📘 Урок {lesson}";

            list.Add(new[] { new KeyboardButton(title) });
        }

        list.Add(new[]
        {
            new KeyboardButton(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад")
        });

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Диабет мектебі" : "Школа диабета",
            replyMarkup: new ReplyKeyboardMarkup(list)
            {
                ResizeKeyboard = true
            },
            cancellationToken: ct);
    }

    // ============================================================
    // HANDLE TEXT
    // ============================================================
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
    {
        if (text == "⬅️ Назад" || text == "⬅️ Артқа")
        {
            user.Phase = BotPhase.MainMenu;
            return;
        }

        // Example: 📘 Урок 1
        if (text.StartsWith("📘"))
        {
            string num = new string(text.Where(char.IsDigit).ToArray());
            if (int.TryParse(num, out int lesson))
            {
                user.CurrentLesson = lesson;
                user.LessonPage = 0;

                await ShowLessonPageAsync(user, chatId, ct);
            }
            return;
        }

        if (text == "➡️ Далее" || text == "➡️ Келесі")
        {
            user.LessonPage++;
            await ShowLessonPageAsync(user, chatId, ct);
            return;
        }

        if (text == "⬅️ Назад" || text == "⬅️ Артқа")
        {
            user.LessonPage--;
            await ShowLessonPageAsync(user, chatId, ct);
            return;
        }

        if (text == "📚 В меню школы" || text == "📚 Мәзірге оралу")
        {
            await ShowMainMenuAsync(user, chatId, ct);
            return;
        }
    }

    // ============================================================
    // SHOW PAGE
    // ============================================================
    public async Task ShowLessonPageAsync(UserData user, long chatId, CancellationToken ct)
    {
        var lessons = GetLessons(user.Language);
        string lid = user.CurrentLesson.ToString();

        if (!lessons.ContainsKey(lid))
        {
            await _bot.SendMessage(chatId, "Ошибка: урок не найден", cancellationToken: ct);
            return;
        }

        var pages = lessons[lid]
            .OrderBy(k => double.Parse(k.Key.Replace($"{lid}.", "")))
            .ToList();

        if (user.LessonPage < 0) user.LessonPage = 0;
        if (user.LessonPage >= pages.Count) user.LessonPage = pages.Count - 1;

        string content = pages[user.LessonPage].Value;

        var menu = new List<KeyboardButton[]>();

        if (user.LessonPage > 0)
            menu.Add(new[]
            {
                new KeyboardButton(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад")
            });

        if (user.LessonPage < pages.Count - 1)
            menu.Add(new[]
            {
                new KeyboardButton(user.Language == "kz" ? "➡️ Келесі" : "➡️ Далее")
            });

        menu.Add(new[]
        {
            new KeyboardButton(user.Language == "kz" ? "📚 Мәзірге оралу" : "📚 В меню школы")
        });

        await _bot.SendMessage(chatId, content,
            replyMarkup: new ReplyKeyboardMarkup(menu)
            {
                ResizeKeyboard = true
            },
            cancellationToken: ct);
    }
}
