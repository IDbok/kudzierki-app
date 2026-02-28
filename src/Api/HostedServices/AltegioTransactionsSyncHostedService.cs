using Infrastructure.Integrations.Altegio;
using Infrastructure.Services;
using Microsoft.Extensions.Options;
using Shared.Abstractions;

namespace Api.HostedServices;

public sealed class AltegioTransactionsSyncHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITimeProvider _timeProvider;
    private readonly AltegioTransactionsSyncSettings _settings;
    private readonly ILogger<AltegioTransactionsSyncHostedService> _logger;

    public AltegioTransactionsSyncHostedService(
        IServiceScopeFactory scopeFactory,
        ITimeProvider timeProvider,
        IOptions<AltegioTransactionsSyncSettings> settings,
        ILogger<AltegioTransactionsSyncHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _settings = settings.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_settings.Enabled)
        {
            _logger.LogInformation("Altegio transactions sync hosted service is disabled.");
            return;
        }

        var nextFullSyncAtUtc = EnsureUtc(_timeProvider.UtcNow);
        _logger.LogInformation(
            "Altegio transactions sync hosted service started. Polling every {PollingMinutes} minutes.",
            _settings.PollingIntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var nowUtc = EnsureUtc(_timeProvider.UtcNow);
                var todayUtc = DateOnly.FromDateTime(nowUtc);

                var shortFrom = todayUtc.AddDays(_settings.ShortFromDaysOffset);
                var shortTo = todayUtc.AddDays(_settings.ShortToDaysOffset);
                await RunSyncAsync(shortFrom, shortTo, "short", reconcileDeleted: false, stoppingToken);

                if (nowUtc >= nextFullSyncAtUtc)
                {
                    var fullFrom = todayUtc.AddDays(_settings.FullFromDaysOffset);
                    var fullTo = todayUtc.AddDays(_settings.FullToDaysOffset);
                    await RunSyncAsync(fullFrom, fullTo, "full", reconcileDeleted: true, stoppingToken);

                    nextFullSyncAtUtc = nowUtc.AddHours(_settings.FullSyncIntervalHours);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Altegio transactions sync iteration failed.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromMinutes(_settings.PollingIntervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task RunSyncAsync(
        DateOnly from,
        DateOnly to,
        string mode,
        bool reconcileDeleted,
        CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var ingestionService = scope.ServiceProvider.GetRequiredService<IAltegioTransactionIngestionService>();
        var result = await ingestionService.IngestFinanceTransactionsAsync(from, to, reconcileDeleted, cancellationToken);

        _logger.LogInformation(
            "Altegio {Mode} sync completed for {From}..{To}. Reconcile deleted: {ReconcileDeleted}. Fetched: {Fetched}, Distinct: {Distinct}, Raw inserted: {RawInserted}, Snapshots inserted: {SnapshotInserted}, Snapshots updated: {SnapshotUpdated}, Deleted marked: {DeletedMarked}, Deleted restored: {DeletedRestored}",
            mode,
            result.From,
            result.To,
            reconcileDeleted,
            result.FetchedCount,
            result.DistinctExternalIdsCount,
            result.RawInsertedCount,
            result.SnapshotInsertedCount,
            result.SnapshotUpdatedCount,
            result.DeletedMarkedCount,
            result.DeletedRestoredCount);
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
}
