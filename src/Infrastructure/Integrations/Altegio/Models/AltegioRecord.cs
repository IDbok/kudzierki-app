using System.Text.Json.Serialization;

namespace Infrastructure.Integrations.Altegio.Models;

public sealed class AltegioRecord
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("date")]
    public DateTime Date { get; init; }

    [JsonPropertyName("datetime")]
    public DateTime DateTime { get; init; }

    [JsonPropertyName("visit_attendance")]
    public int VisitAttendance { get; init; }

    [JsonPropertyName("deleted")]
    public bool Deleted { get; init; }

    [JsonPropertyName("client")]
    public AltegioClient? Client { get; init; }

    [JsonPropertyName("services")]
    public List<AltegioRecordService> Services { get; init; } = [];
}
