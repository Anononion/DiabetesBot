namespace DiabetesBot.Models;

public class UserData
{
    public long UserId { get; set; }

    // текущий язык
    public string Language { get; set; } = "ru";

    // текущая фаза
    public BotPhase Phase { get; set; } = BotPhase.MainMenu;

    // сохранённая история измерений
    public List<Measurement> Measurements { get; set; } = new();

    // дневник еды
    public List<FoodEntry> FoodDiary { get; set; } = new();

    // история хлебных единиц
    public List<XeRecord> XeHistory { get; set; } = new();

    public string? TempMeasurementType { get; set; }

    public string? TempSelectedFoodId { get; set; }

    public int CurrentLesson { get; set; } = 0;
    public int LessonPage { get; set; } = 0;

    // ======================================================
    // ▶ ДОБАВЬ ЭТИ ТРИ ПОЛЯ ← ЭТО ГЛАВНАЯ ПРОБЛЕМА
    // ======================================================
    public GlucoseData? Glucose { get; set; }       // используется GlucoseModule
    public string? TempLessonId { get; set; }       // используется DiabetesSchoolModule
    public string? TempSubId { get; set; }          // используется DiabetesSchoolModule

    // используется GlucoseModule — список записей глюкозы
    public List<GlucoseEntry> Glucose { get; set; } = new();

    // используется DiabetesSchoolModule — выбранный урок
    public string? TempLessonId { get; set; }

    // используется DiabetesSchoolModule — выбранная страница урока
    public string? TempSubId { get; set; }

}

