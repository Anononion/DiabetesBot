using System.Text.Json.Serialization;

public class FoodItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("carbsPer100")]
    public int CarbsPer100 { get; set; }
}
