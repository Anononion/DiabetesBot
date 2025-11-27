public enum BotPhase
{
    // ===============================
    // ОСНОВНОЕ
    // ===============================
    MainMenu,

    Settings,
    LanguageChoice,

    // ===============================
    // ГЛЮКОЗА
    // ===============================
    Glucose,                // Главное меню модуля "Глюкоза"
    Glucose_SelectType,    // Ожидание выбора типа (до еды, после еды и т.д.)
    Glucose_InputValue,    // Ожидание ввода числа

    // ===============================
    // ХЛЕБНЫЕ ЕДИНИЦЫ (ХЕ)
    // ===============================
    BreadUnits,                        // Главное меню ХЕ
    BreadUnits_SelectCategory,         // Выбор категории продукта
    BreadUnits_SelectItem,             // Выбор конкретного продукта
    BreadUnits_InputGrams,             // Ввод граммов

    // ===============================
    // ШКОЛА ДИАБЕТА
    // ===============================
    DiabetesSchool,                   // Главное меню школы диабета
    DiabetesSchool_SelectLesson,      // Выбор урока
    DiabetesSchool_SelectPage         // Выбор страницы внутри урока

    None = 0,

    // === Glucose ===
    Glucose_ValueInput = 10,

    // === Xe ===
    BreadUnits_EnterGrams = 20,

    // === School ===
    School_LessonNavigation = 30
}
