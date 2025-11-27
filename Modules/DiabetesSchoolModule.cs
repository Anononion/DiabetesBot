using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using DiabetesBot.Models;

namespace DiabetesBot.Modules;

public class DiabetesSchoolModule
{
    private readonly ITelegramBotClient _bot;

    // lessons[lessonId][subId] = text
    private Dictionary<string, Dictionary<string, string>> _lessonsRu = new();
    private Dictionary<string, Dictionary<string, string>> _lessonsKz = new();

    public DiabetesSchoolModule(ITelegramBotClient bot)
    {
        _bot = bot;
        Load();
    }

    private void Load()
    {
        // Загружаем RU
        var langRu = File.ReadAllText("Data/lang_ru.json");
        dynamic ru = JsonConvert.DeserializeObject(langRu)!;
        _lessonsRu = ru["ds.lessons"].ToObject<Dictionary<string, Dictionary<string, string>>>();

        // Загружаем KZ
        var langKz = File.ReadAllText("Data/lang_kk.json");
        dynamic kz = JsonConvert.DeserializeObject(langKz)!;
        _lessonsKz = kz["ds.lessons"].ToObject<Dictionary<string, Dictionary<string, string>>>();
    }

    private Dictionary<string, Dictionary<string, string>> GetBlock(string lang)
        => lang == "kz" ? _lessonsKz : _lessonsRu;

    // ====================================================================
    // MAIN MENU (Уроки 1,2,3)
    // ====================================================================
    public async Task ShowMainMenuAsync(UserData user, long chatId, CancellationToken ct)
    {
        var lang = user.Language == "kz" ? "Сабақ " : "Урок ";

        var buttons = new List<KeyboardButton[]>();

        foreach (var block in GetBlock(user.Language).Keys)
        {
            buttons.Add(new[] { new KeyboardButton($"{lang}{block}") });
        }

        buttons.Add(new[] { new KeyboardButton(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад") });

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Диабет мектебі:" : "Школа диабета:",
            replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true },
            cancellationToken: ct);
    }

    // ====================================================================
    // Показать список подуроков: 1.1, 1.2, 1.3...
    // ====================================================================
    public async Task ShowLessonMenuAsync(UserData user, long chatId, int lesson, CancellationToken ct)
    {
        var lessons = GetBlock(user.Language);

        if (!lessons.ContainsKey(lesson.ToString()))
        {
            await _bot.SendMessage(chatId, "Ошибка урока", cancellationToken: ct);
            return;
        }

        var buttons = new List<KeyboardButton[]>();

        foreach (var sub in lessons[lesson.ToString()].Keys)
        {
            buttons.Add(new[] { new KeyboardButton(sub) });
        }

        buttons.Add(new[] { new KeyboardButton(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад") });

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? $"Сабақ {lesson}:" : $"Урок {lesson}:",
            replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true },
            cancellationToken: ct);
    }

    // ====================================================================
    // Показать содержимое подурока
    // ====================================================================
    public async Task ShowLessonPageAsync(UserData user, long chatId, string lessonId, string subId, CancellationToken ct)
    {
        var lessons = GetBlock(user.Language);

        if (!lessons.ContainsKey(lessonId) ||
            !lessons[lessonId].ContainsKey(subId))
        {
            await _bot.SendMessage(chatId, "Ошибка данных урока", cancellationToken: ct);
            return;
        }

        string text = lessons[lessonId][subId];

        var buttons = new List<KeyboardButton[]>
        {
            new[]
            {
                new KeyboardButton(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад"),
                new KeyboardButton(user.Language == "kz" ? "Далее" : "Далее")
            }
        };

        await _bot.SendMessage(chatId,
            text,
            replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true },
            cancellationToken: ct);
    }
    public async Task HandleTextAsync(UserData user, Message msg, CancellationToken ct)
{
    // ВРЕМЕННАЯ ЗАГЛУШКА — чтобы сборка прошла!
    // Потом заменим реальной логикой.
    await _bot.SendMessage(msg.Chat.Id,
        user.Language == "kz" 
            ? "Қате команда. Сабақты таңдаңыз." 
            : "Неизвестная команда. Выберите урок.",
        cancellationToken: ct);
}

}

