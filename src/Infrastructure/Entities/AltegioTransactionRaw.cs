namespace Infrastructure.Entities;

public class AltegioTransactionRaw
{
    public Guid Id { get; set; }

    public long ExternalId { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public string PayloadHash { get; set; } = string.Empty;

    public DateTimeOffset FetchedAtUtc { get; set; }
}
