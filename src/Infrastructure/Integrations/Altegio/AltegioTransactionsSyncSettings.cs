namespace Infrastructure.Integrations.Altegio;

public sealed class AltegioTransactionsSyncSettings
{
    public const string SectionName = "TransactionsSync";

    public bool Enabled { get; set; } = false;

    public int PollingIntervalMinutes { get; set; } = 60;

    public int ShortFromDaysOffset { get; set; } = -1;

    public int ShortToDaysOffset { get; set; } = 10;

    public int FullFromDaysOffset { get; set; } = -10;

    public int FullToDaysOffset { get; set; } = 180;

    public int FullSyncIntervalHours { get; set; } = 24;
}
