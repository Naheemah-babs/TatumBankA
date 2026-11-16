using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TatumWelcome2.BLL.IServices;
using TatumWelcome2.Domain.Models;

namespace TatumWelcome2.TransactionReconciliation.Processor;

/// <summary>
/// Background loop that settles Pending interbank transfers:
/// every N seconds it asks the other bank what happened to each one, then
/// marks it Successful or reverses (refunds) it. All the actual rules live in
/// IInterbankOutgoingTransferService; this class only decides WHEN to run it.
/// </summary>
public sealed class ReconciliationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReconciliationWorker> _logger;
    private readonly ReconciliationOptions _options;
    private readonly WorkPartition _partition;

    public ReconciliationWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<ReconciliationOptions> options,
        ILogger<ReconciliationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;

        _partition = new WorkPartition(_options.PartitionIndex, _options.PartitionCount);
        _partition.EnsureValid(); // fail at startup if the slice settings are nonsense
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        TimeSpan interval = TimeSpan.FromSeconds(Math.Max(1, _options.IntervalSeconds));

        _logger.LogInformation(
            "Reconciliation worker started. Partition {Partition}, every {Seconds}s.",
            _partition, interval.TotalSeconds);

        // Run once immediately so we do not wait a full interval after startup.
        await RunOnePassAsync(stoppingToken);

        using var timer = new PeriodicTimer(interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await RunOnePassAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown (Ctrl+C). Nothing to do.
        }

        _logger.LogInformation("Reconciliation worker stopped.");
    }

    private async Task RunOnePassAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The service is registered as SCOPED, so we must create a scope per pass
            
            using IServiceScope scope = _scopeFactory.CreateScope();

            var service = scope.ServiceProvider
                .GetRequiredService<IInterbankOutgoingTransferService>();

            await service.ReconcilePendingTransfersAsync(_partition, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw; // let the loop end cleanly
        }
        catch (Exception ex)
        {
            // A failed pass (database down, etc.) must NOT kill the worker.
            // Log it and try again on the next tick.
            _logger.LogError(ex, "Reconciliation pass failed; will retry on the next tick.");
        }
    }
}