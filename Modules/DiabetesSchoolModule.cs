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
    public async Task HandleTextAsync(UserData user, long chatId, string text, CancellationToken ct)
{
    var lessons = GetBlock(user.Language);

    // === Вернуться назад в главное меню школы ===
    if (text == "⬅️ Назад" || text == "⬅️ Артқа")
    {
        user.Phase = BotPhase.MainMenu;
        await ShowMainMenuAsync(user, chatId, ct);
        return;
    }

    // === Выбор урока: "Урок 1" / "Сабақ 1" ===
    if (text.StartsWith("Урок") || text.StartsWith("Сабақ"))
    {
        var num = text.Split(' ').Last();
        if (int.TryParse(num, out int lesson))
        {
            user.TempLessonId = lesson.ToString();
            user.Phase = BotPhase.DiabetesSchool;
            await ShowLessonMenuAsync(user, chatId, lesson, ct);
            return;
        }
    }

    // === Выбор подурока: например "1.2", "2.3" ===
    if (text.Contains('.'))
    {
        var parts = text.Split('.');
        if (parts.Length == 2)
        {
            string lessonId = parts[0];
            string subId = parts[1];

            user.TempLessonId = lessonId;
            user.TempSubId = subId;

            await ShowLessonPageAsync(user, chatId, lessonId, subId, ct);
            return;
        }
    }

    // === Нажата кнопка "Далее" ===
    if (text == "Далее" || text == "Алға")
    {
        if (user.TempLessonId != null && user.TempSubId != null)
        {
            var lessonsDict = GetBlock(user.Language);

            var subs = lessonsDict[user.TempLessonId].Keys.ToList();
            int index = subs.IndexOf(user.TempSubId);

            if (index + 1 < subs.Count)
            {
                string nextSub = subs[index + 1];
                user.TempSubId = nextSub;

                await ShowLessonPageAsync(user, chatId, user.TempLessonId, nextSub, ct);
                return;
            }
        }

        await _bot.SendMessage(chatId, 
            user.Language == "kz" ? "Сабақ аяқталды." : "Урок завершён.", 
            cancellationToken: ct);

        return;
    }

    // === Если ничего не подошло ===
    await _bot.SendMessage(chatId,
        user.Language == "kz" ? "Бұйрық түсініксіз." : "Команда не распознана.",
        cancellationToken: ct);
}


}





