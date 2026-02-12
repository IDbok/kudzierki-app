using System.Text.Json.Serialization;

namespace Infrastructure.Integrations.Altegio.Models;

public sealed class AltegioEnvelope<T>
{
    [JsonPropertyName("data")]
    public List<T> Data { get; init; } = [];
}
