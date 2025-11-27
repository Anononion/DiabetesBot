using System.Text.Json.Serialization;

namespace DiabetesBot.Models;

public class FoodItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("name_ru")]
    public string Name_Ru { get; set; } = "";

    [JsonPropertyName("name_kk")]
    public string Name_Kk { get; set; } = "";

    [JsonPropertyName("gramsPerXE")]
    public double GramsPerXE { get; set; }

    [JsonPropertyName("carbsPer100")]
    public double CarbsPer100 { get; set; }

    // 🔥 ДОБАВЛЯЕМ ОБЩЕЕ ИМЯ ДЛЯ СТАРОГО КОДА
    [JsonIgnore]
    public string Name => Name_Ru; // чтобы старый код не падал
}
