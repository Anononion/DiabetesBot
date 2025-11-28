using DiabetesBot.Modules;

namespace DiabetesBot.Models;

public class UserData
{
    public long UserId { get; set; }

    // текущий язык
    public string Language { get; set; } = "ru";

    // текущая фаза
    public BotPhase Phase { get; set; } = BotPhase.MainMenu;

    // --- замеры ---
    public List<Measurement> Measurements { get; set; } = new();

    // --- дневник еды ---
    public List<FoodEntry> FoodDiary { get; set; } = new();

    // --- хлебные единицы ---
    public List<XeRecord> XeHistory { get; set; } = new();

    public string? TempMeasurementType { get; set; }
    public string? TempSelectedFoodId { get; set; }

    // --- школа диабета ---
    public int CurrentLesson { get; set; } = 0;
    public int LessonPage { get; set; } = 0;
    public int CurrentSub { get; set; } = 0;


    public string? TempLessonId { get; set; }      // НЕ ДУБЛИРУЕМ
    public string? TempSubId { get; set; }         // НЕ ДУБЛИРУЕМ

    // --- ГЛЮКОЗА ---
    public List<GlucoseRecord> Glucose { get; set; } = new();
    public FoodItem? SelectedFood { get; set; }
    public string? TempGlucoseType { get; set; }
    public string? TempGlucoseType { get; set; }
}







