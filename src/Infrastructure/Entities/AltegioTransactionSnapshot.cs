namespace Infrastructure.Entities;

public class AltegioTransactionSnapshot
{
    public Guid Id { get; set; }

    public long ExternalId { get; set; }

    public DateTime? AppointmentDateTime { get; set; }

    public DateTime? AltegioCreateDateTime { get; set; }

    public DateTime? LastChangeDateTime { get; set; }

    public decimal Amount { get; set; }

    public string? Comment { get; set; }

    public int? AccountId { get; set; }

    public string? AccountTitle { get; set; }

    public bool IsCash { get; set; }

    public bool IsDeletedInSource { get; set; }

    public DateTimeOffset? DeletedDetectedAtUtc { get; set; }

    public DateTimeOffset FirstSeenAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }
}
