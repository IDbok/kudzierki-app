using System.Text.Json.Serialization;

namespace Infrastructure.Integrations.Altegio.Models;

public sealed class AltegioTransaction
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; init; }

    [JsonPropertyName("comment")]
    public string? Comment { get; init; }

    [JsonPropertyName("account")]
    public AltegioAccount? Account { get; init; }
}
