namespace Infrastructure.Services;

public interface IAltegioTransactionIngestionService
{
    Task<AltegioTransactionIngestionResult> IngestFinanceTransactionsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);
}

public sealed record AltegioTransactionIngestionResult(
    DateOnly From,
    DateOnly To,
    int FetchedCount,
    int DistinctExternalIdsCount,
    int RawInsertedCount,
    int SnapshotInsertedCount,
    int SnapshotUpdatedCount);
