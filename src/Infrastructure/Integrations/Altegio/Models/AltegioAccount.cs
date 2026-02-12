using System.Text.Json.Serialization;

namespace Infrastructure.Integrations.Altegio.Models;

public sealed class AltegioAccount
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("is_cash")]
    public bool IsCash { get; init; }
}
