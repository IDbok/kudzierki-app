using System.Text.Json.Serialization;

namespace Infrastructure.Integrations.Altegio.Models;

public sealed class AltegioTransaction
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("date")]
    [JsonConverter(typeof(AltegioDateTimeJsonConverter))]
    public DateTime Date { get; init; }

    [JsonPropertyName("datetime")]
    [JsonConverter(typeof(AltegioDateTimeJsonConverter))]
    public DateTime DateTime { get; init; }

    [JsonPropertyName("create_date")]
    [JsonConverter(typeof(AltegioDateTimeJsonConverter))]
    public DateTime CreatedAt { get; init; }

    [JsonPropertyName("last_change_date")]
    [JsonConverter(typeof(AltegioDateTimeJsonConverter))]
    public DateTime LastChangeDate { get; init; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("comment")]
    public string? Comment { get; init; }

    [JsonPropertyName("account")]
    public AltegioAccount? Account { get; init; }
}
