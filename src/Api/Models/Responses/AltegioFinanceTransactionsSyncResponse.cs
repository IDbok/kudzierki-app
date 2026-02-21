namespace Api.Models.Responses;

public sealed record AltegioFinanceTransactionsSyncResponse(
    DateOnly From,
    DateOnly To,
    int FetchedCount,
    int DistinctExternalIdsCount,
    int RawInsertedCount,
    int SnapshotInsertedCount,
    int SnapshotUpdatedCount);
