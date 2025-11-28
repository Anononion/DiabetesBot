using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

using Newtonsoft.Json;
using File = System.IO.File;

using DiabetesBot.Models;
using DiabetesBot.Services;
using DiabetesBot.Utils;

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
        var ru = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("Data/lang_ru.json"));
        _lessonsRu = ru["ds.lessons"].ToObject<Dictionary<string, Dictionary<string, string>>>();

        var kz = JsonConvert.DeserializeObject<dynamic>(File.ReadAllText("Data/lang_kk.json"));
        _lessonsKz = kz["ds.lessons"].ToObject<Dictionary<string, Dictionary<string, string>>>();
    }

    private Dictionary<string, Dictionary<string, string>> GetBlock(string lang)
        => lang == "kz" ? _lessonsKz : _lessonsRu;

    // ============================================================
    // ГЛАВНОЕ МЕНЮ УРОКОВ
    // ============================================================
    public async Task ShowLessonsAsync(UserData user, long chatId, CancellationToken ct)
    {
        var lessons = GetBlock(user.Language);

        var buttons = new List<KeyboardButton[]>();
        string prefix = user.Language == "kz" ? "Сабақ " : "Урок ";

        foreach (var id in lessons.Keys)
            buttons.Add(new[] { new KeyboardButton(prefix + id) });

        buttons.Add(new[] { new KeyboardButton(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад") });

        await _bot.SendMessage(chatId,
            user.Language == "kz" ? "Сабақты таңдаңыз:" : "Выберите урок:",
            replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true },
            cancellationToken: ct);
    }

    // ============================================================
    // МЕНЮ ПОДУРОКОВ 1.1 / 1.2 / 1.3
    // ============================================================
    public async Task ShowSubLessonsAsync(UserData user, long chatId, int lessonId, CancellationToken ct)
    {
        var lessons = GetBlock(user.Language);

        if (!lessons.ContainsKey(lessonId.ToString()))
        {
            await _bot.SendMessage(chatId, "Ошибка данных", cancellationToken: ct);
            return;
        }

        user.CurrentLesson = lessonId;

        var subIds = lessons[lessonId.ToString()].Keys;

        var buttons = subIds
            .Select(s => new[] { new KeyboardButton($"{lessonId}.{s}") })
            .ToList();

        buttons.Add(new[] { new KeyboardButton(user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад") });

        await _bot.SendMessage(chatId,
            $"{(user.Language == "kz" ? "Сабақ" : "Урок")} {lessonId}",
            replyMarkup: new ReplyKeyboardMarkup(buttons) { ResizeKeyboard = true },
            cancellationToken: ct);
    }

    // ============================================================
    // ПОКАЗ СТРАНИЦЫ ПОДУРОКА
    // ============================================================
    public async Task ShowPageAsync(UserData user, long chatId, int lessonId, int subId, CancellationToken ct)
    {
        var lessons = GetBlock(user.Language);

        string l = lessonId.ToString();
        string s = subId.ToString();

        if (!lessons.ContainsKey(l) || !lessons[l].ContainsKey(s))
        {
            await _bot.SendMessage(chatId, "Ошибка данных.", cancellationToken: ct);
            return;
        }

        user.CurrentLesson = lessonId;
        user.CurrentSub = subId;

        string text = lessons[l][s];

        var kb = new ReplyKeyboardMarkup(new[]
        {
            new KeyboardButton[]
            {
                user.Language == "kz" ? "⬅️ Артқа" : "⬅️ Назад",
                user.Language == "kz" ? "Келесі" : "Далее"
            }
        })
        { ResizeKeyboard = true };

        await _bot.SendMessage(chatId, text, replyMarkup: kb, cancellationToken: ct);
    }

    // ============================================================
    // КНОПКА "ДАЛЕЕ"
    // ============================================================
    public async Task ShowNextPageAsync(UserData user, long chatId, CancellationToken ct)
    {
        int lessonId = user.CurrentLesson;
        int subId = user.CurrentSub + 1;

        var lessons = GetBlock(user.Language);

        if (!lessons.ContainsKey(lessonId.ToString())
            || !lessons[lessonId.ToString()].ContainsKey(subId.ToString()))
        {
            await _bot.SendMessage(chatId,
                user.Language == "kz" ? "Бұл сабақ аяқталды." : "Этот урок окончен.",
                cancellationToken: ct);

            await ShowLessonsAsync(user, chatId, ct);
            user.Phase = BotPhase.DiabetesSchool;
            return;
        }

        await ShowPageAsync(user, chatId, lessonId, subId, ct);
    }
}
