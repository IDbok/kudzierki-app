using System.Text.Json.Serialization;

namespace Infrastructure.Integrations.Altegio.Models;

public sealed class AltegioClient
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("surname")]
    public string? Surname { get; init; }
}
