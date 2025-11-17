namespace DiabetesBot.Models;


public enum UserPhase
{
    New,
    ChoosingLanguage,

    MainMenu,

    // Глюкометрия
    GlucoseMenu,
    AwaitGlucoseValue,

    // ХЕ
    BreadUnits,

    DiabetesSchool,
    Settings
}
public class UserState
{
    public UserStep Step { get; set; } = UserStep.None;
    public Dictionary<string, string> Temp { get; set; } = new();
}

