namespace DiabetesBot.Models
{
    public class XeRecord
    {
        /// <summary>
        /// ID продукта (из foods.json)
        /// </summary>
        public string ProductId { get; set; } = "";

        /// <summary>
        /// Имя продукта на момент записи (для истории)
        /// </summary>
        public string ProductName { get; set; } = "";

        /// <summary>
        /// Количество граммов, введённое пользователем
        /// </summary>
        public double Grams { get; set; }

        /// <summary>
        /// Рассчитанные хлебные единицы (ХЕ)
        /// </summary>
        public double XE { get; set; }

        /// <summary>
        /// Время записи
        /// </summary>
        public DateTime Time { get; set; }
    }
}
