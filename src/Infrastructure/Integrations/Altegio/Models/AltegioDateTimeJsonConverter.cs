using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Integrations.Altegio.Models;

internal sealed class AltegioDateTimeJsonConverter : JsonConverter<DateTime>
{
    private static readonly string[] SupportedFormats =
    [
        "yyyy-MM-dd",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ssZ",
        "dd.MM.yyyy",
        "dd.MM.yyyy HH:mm:ss"
    ];

    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var raw = reader.GetString();
            if (string.IsNullOrWhiteSpace(raw))
                return default;

            if (DateTime.TryParseExact(raw, SupportedFormats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var exact))
                return exact;

            if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed))
                return parsed;

            if (long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixFromString))
                return ParseUnix(unixFromString);

            throw new JsonException($"Unable to parse DateTime value '{raw}'.");
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var unix))
            return ParseUnix(unix);

        throw new JsonException($"Unsupported token '{reader.TokenType}' for DateTime conversion.");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }

    private static DateTime ParseUnix(long value)
    {
        var absolute = Math.Abs(value);
        return absolute >= 100000000000
            ? DateTimeOffset.FromUnixTimeMilliseconds(value).UtcDateTime
            : DateTimeOffset.FromUnixTimeSeconds(value).UtcDateTime;
    }
}
