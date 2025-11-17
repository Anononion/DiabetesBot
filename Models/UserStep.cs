namespace DiabetesBot.Models;

public enum UserStep
{
    None,

    // Глюкометрия
    AwaitGlucoseValue,

    // Хлебные единицы
    BU_WaitProductName,
    BU_WaitWeight,
    GlucoseAwaitValue
}
