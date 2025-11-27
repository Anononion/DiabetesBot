using System.Text.Json.Serialization;

namespace DiabetesBot.Models;

public class FoodItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name_ru")]
    public string name_ru { get; set; } = "";

    [JsonPropertyName("name_kk")]
    public string name_kk { get; set; } = "";

    [JsonPropertyName("carbsPer100")]
    public double carbsPer100 { get; set; }
}
