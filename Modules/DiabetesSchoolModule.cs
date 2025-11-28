using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

using Newtonsoft.Json;
using File = System.IO.File;

using DiabetesBot.Utils;
using DiabetesBot.Models;
using DiabetesBot.Services;

namespace DiabetesBot.Modules;

public class DiabetesSchoolModule
{
    private readonly ITelegramBotClient _bot;

    // lessons[chapter][sublesson] = text
    private Dictionary<string, Dictionary<string, string>> _lessonsRu = new();
    private Dictionary<string, Dictionary<string, string>> _lessonsKz = new();

    public DiabetesSchoolModule(ITelegramBotClient bot)
    {
        _bot = bot;
        Load();
    }

    private void Load()
    {
        // RU
        var rawRu = File.ReadAllText("Data/lang_ru.json");
        dynamic ruJson = JsonConvert.DeserializeObject(rawRu)!;
        _lessonsRu = ruJson["ds.lessons"].ToObject<Dictionary<string, Dictionary<string, string>>>();

        // KZ
        var rawKz = File.ReadAllText("Data/lang_kk.json");
        dynamic kzJson = JsonConvert.DeserializeObject(rawKz)!;
        _lessonsKz = kzJson["ds.lessons"].ToObject<Dictionary<string, Dictionary<string, string>>>();
    }

    private Dictionary<string, Dictionary<string, string>> GetLessons(string lang)
        => lang == "kz" ? _lessonsKz : _lessonsRu;

    // ============================================================
    // Главное меню школы (1,2,3,4)
    // ============================================================
    public async Task ShowMainMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        string prefix = user.Language == "kz" ? "Сабақ " : "Урок ";

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { $"{prefix}1" },
            new KeyboardButton[] { $"{prefix}2" },
            new KeyboardButton[] { $"{prefix}3" },
            new KeyboardButton[] { $"{prefix}4" },
            new KeyboardButton[] { user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад" }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Диабет мектебі:" : "Школа диабета:",
            replyMarkup: kb,
            cancellationToken: ct);
    }

    // ============================================================
    // Показать уроки внутри главы
    // ============================================================
    public async Task ShowLessonsAsync(UserData user, long chatId, int lesson, CancellationToken ct)
    {
        var lessons = GetLessons(user.Language);

        if (!lessons.ContainsKey(lesson.ToString()))
        {
            await _bot.SendMessage(chatId, "Ошибка данных урока.", cancellationToken: ct);
            return;
        }

        var buttons = lessons[lesson.ToString()].Keys
            .Select(k => new[] { new KeyboardButton(k) })
            .ToList();

        buttons.Add(new[] { new KeyboardButton(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад") });

        await _bot.SendMessage(chatId,
            (user.Language == "kz" ? "Сабақ " : "Урок ") + lesson,
            replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true },
            cancellationToken: ct);
    }

    // ============================================================
    // Показать содержание подурока
    // ============================================================
    public async Task ShowLessonTextAsync(UserData user, long chatId, string lessonId, CancellationToken ct)
    {
        var lessons = GetLessons(user.Language);

        string chapter = lessonId.Split('.')[0];

        if (!lessons.ContainsKey(chapter) || !lessons[chapter].ContainsKey(lessonId))
        {
            await _bot.SendMessage(chatId, "Ошибка данных.", cancellationToken: ct);
            return;
        }

        string text = lessons[chapter][lessonId];

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[] { user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад" }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId, text, replyMarkup: kb, cancellationToken: ct);
    }


    // ============================================================
    // CALLBACK HANDLER (старый стиль)
    // ============================================================
    public async Task HandleCallbackAsync(UserData user, CallbackQuery query, CancellationToken ct)
    {
        string data = query.Data ?? "";

        if (!data.StartsWith("DS_LESSON|"))
            return;

        string lessonId = data.Split('|')[1];

        await ShowLessonTextAsync(user, query.Message!.Chat.Id, lessonId, ct);
    }
}
