using System.Security.Cryptography;
using System.Text;
using Infrastructure.Data;
using Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Abstractions;

namespace Infrastructure.Services;

public sealed class AltegioTransactionIngestionService : IAltegioTransactionIngestionService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAltegioService _altegioService;
    private readonly ITimeProvider _timeProvider;
    private readonly ILogger<AltegioTransactionIngestionService> _logger;

    public AltegioTransactionIngestionService(
        ApplicationDbContext dbContext,
        IAltegioService altegioService,
        ITimeProvider timeProvider,
        ILogger<AltegioTransactionIngestionService> logger)
    {
        _dbContext = dbContext;
        _altegioService = altegioService;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AltegioTransactionIngestionResult> IngestFinanceTransactionsAsync(
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var fetchedAtUtc = EnsureUtc(_timeProvider.UtcNow);
        var fetchedAtOffset = new DateTimeOffset(fetchedAtUtc);

        var sourceTransactions = await _altegioService.GetFinanceSourceTransactionsAsync(from, to, cancellationToken);
        var latestTransactionByExternalId = sourceTransactions
            .GroupBy(x => x.Id)
            .Select(g => g.Last())
            .ToList();

        if (latestTransactionByExternalId.Count == 0)
        {
            return new AltegioTransactionIngestionResult(
                from,
                to,
                sourceTransactions.Count,
                0,
                0,
                0,
                0);
        }

        var externalIds = latestTransactionByExternalId.Select(x => x.Id).Distinct().ToArray();

        var existingRawKeys = await _dbContext.AltegioTransactionRaws
            .Where(x => externalIds.Contains(x.ExternalId))
            .Select(x => new { x.ExternalId, x.PayloadHash })
            .ToListAsync(cancellationToken);

        var knownRawKeys = existingRawKeys
            .Select(x => BuildRawKey(x.ExternalId, x.PayloadHash))
            .ToHashSet(StringComparer.Ordinal);

        var rawInserted = 0;

        foreach (var transaction in sourceTransactions)
        {
            var payloadHash = ComputePayloadHash(transaction.PayloadJson);
            var rawKey = BuildRawKey(transaction.Id, payloadHash);
            if (!knownRawKeys.Add(rawKey))
                continue;

            _dbContext.AltegioTransactionRaws.Add(new AltegioTransactionRaw
            {
                Id = Guid.NewGuid(),
                ExternalId = transaction.Id,
                PayloadJson = transaction.PayloadJson,
                PayloadHash = payloadHash,
                FetchedAtUtc = fetchedAtOffset
            });

            rawInserted++;
        }

        var existingSnapshots = await _dbContext.AltegioTransactionSnapshots
            .Where(x => externalIds.Contains(x.ExternalId))
            .ToDictionaryAsync(x => x.ExternalId, cancellationToken);

        var snapshotInserted = 0;
        var snapshotUpdated = 0;

        foreach (var transaction in latestTransactionByExternalId)
        {
            if (!existingSnapshots.TryGetValue(transaction.Id, out var snapshot))
            {
                snapshot = new AltegioTransactionSnapshot
                {
                    Id = Guid.NewGuid(),
                    ExternalId = transaction.Id,
                    FirstSeenAtUtc = fetchedAtOffset
                };

                _dbContext.AltegioTransactionSnapshots.Add(snapshot);
                existingSnapshots[transaction.Id] = snapshot;
                snapshotInserted++;
            }
            else
            {
                snapshot.FirstSeenAtUtc = Min(snapshot.FirstSeenAtUtc, fetchedAtOffset);
                snapshotUpdated++;
            }

            snapshot.AppointmentDateTime = ToNullableDateTime(transaction.AppointmentDateTime);
            snapshot.AltegioCreateDateTime = transaction.AltegioCreateDateTime;
            snapshot.LastChangeDateTime = transaction.LastChangeDateTime;
            snapshot.Amount = transaction.Amount;
            snapshot.Comment = transaction.Comment;
            snapshot.AccountId = transaction.AccountId;
            snapshot.AccountTitle = transaction.AccountTitle;
            snapshot.IsCash = transaction.IsCash;
            snapshot.LastSeenAtUtc = fetchedAtOffset;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Altegio transaction ingest completed for range {From}..{To}. Fetched: {Fetched}, Distinct: {Distinct}, Raw inserted: {RawInserted}, Snapshots inserted: {SnapshotInserted}, Snapshots updated: {SnapshotUpdated}",
            from,
            to,
            sourceTransactions.Count,
            latestTransactionByExternalId.Count,
            rawInserted,
            snapshotInserted,
            snapshotUpdated);

        return new AltegioTransactionIngestionResult(
            from,
            to,
            sourceTransactions.Count,
            latestTransactionByExternalId.Count,
            rawInserted,
            snapshotInserted,
            snapshotUpdated);
    }

    private static string ComputePayloadHash(string payloadJson)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payloadJson));
        return Convert.ToHexString(hash);
    }

    private static string BuildRawKey(long externalId, string payloadHash)
    {
        return $"{externalId}:{payloadHash}";
    }

    private static DateTime EnsureUtc(DateTime dateTime)
    {
        return dateTime.Kind switch
        {
            DateTimeKind.Utc => dateTime,
            DateTimeKind.Local => dateTime.ToUniversalTime(),
            _ => DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)
        };
    }

    private static DateTime? ToNullableDateTime(DateTime value)
    {
        return value == default ? null : value;
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right)
    {
        return left <= right ? left : right;
    }
}
