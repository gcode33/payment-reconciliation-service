namespace Payments.Worker;

// Placeholder until PLAN step 1.7 adds the unknown-outcome resolver.
internal sealed partial class Worker(ILogger<Worker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        LogStarted(logger);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Payments worker started; no jobs registered yet.")]
    private static partial void LogStarted(ILogger logger);
}
