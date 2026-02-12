using System.Text.Json.Serialization;

namespace Infrastructure.Integrations.Altegio.Models;

public sealed class AltegioRecordService
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("cost")]
    public decimal Cost { get; init; }

    [JsonPropertyName("cost_to_pay")]
    public decimal CostToPay { get; init; }
}
