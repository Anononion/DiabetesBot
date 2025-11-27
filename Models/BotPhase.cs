public enum BotPhase
{
    None = 0,

    // ===============================
    // ОСНОВНОЕ
    // ===============================
    MainMenu,
    Settings,
    LanguageChoice,

    // ===============================
    // ГЛЮКОЗА
    // ===============================
    Glucose,                // Главное меню модуля Глюкозы
    Glucose_SelectType,     // Ждём выбора: натощак / после еды / по времени
    Glucose_InputValue,     // Ждём ввода числа

    // ===============================
    // ХЛЕБНЫЕ ЕДИНИЦЫ
    // ===============================
    BreadUnits,
    BreadUnits_SelectCategory,
    BreadUnits_SelectItem,
    BreadUnits_InputGrams,  // Ждём граммы

    // ===============================
    // ШКОЛА ДИАБЕТА
    // ===============================
    DiabetesSchool,
    DiabetesSchool_SelectLesson,
    DiabetesSchool_SelectPage
}
