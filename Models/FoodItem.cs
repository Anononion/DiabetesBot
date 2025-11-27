using Newtonsoft.Json;

namespace DiabetesBot.Models;

public class FoodItem
{
    [JsonProperty("id")]
    public string Id { get; set; }

    [JsonProperty("category")]
    public string Category { get; set; }

    [JsonProperty("name_ru")]
    public string NameRu { get; set; }

    [JsonProperty("name_kk")]
    public string NameKk { get; set; }

    [JsonProperty("carbsPer100")]
    public int CarbsPer100 { get; set; }

    [JsonProperty("gramsPerXE")]
    public int GramsPerXE { get; set; }
}
