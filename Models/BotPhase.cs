namespace DiabetesBot.Models
{
    public enum BotPhase
    {
        // Базовое состояние
        None = 0,

        // Главное меню и настройки
        MainMenu        = 1,
        Settings        = 2,
        LanguageChoice  = 3,

        // Блок глюкозы
        Glucose             = 10,
        Glucose_ValueInput  = 11,
        Glucose_ValueInputType = 12,
        Glucose_ValueInputType  = 13,

        // Хлебные единицы
        BreadUnits              = 20,
        BreadUnits_EnterGrams   = 21,

        // Школа диабета
        DiabetesSchool      = 30
    }
}
