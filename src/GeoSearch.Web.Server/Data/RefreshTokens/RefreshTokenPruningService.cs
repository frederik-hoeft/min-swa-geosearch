using GeoSearch.Web.Server.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace GeoSearch.Web.Server.Data.RefreshTokens;

/// <summary>Removes expired session history after its bounded replay-detection retention period.</summary>
internal sealed class RefreshTokenPruningService(
    IServiceScopeFactory scopeFactory,
    IOptions<RefreshTokenCleanupOptions> options,
    TimeProvider timeProvider,
    ILogger<RefreshTokenPruningService> logger) : BackgroundService
{
    private static readonly Action<ILogger, int, Exception?> s_historyPruned = LoggerMessage.Define<int>(
        LogLevel.Information, new EventId(1001, "RefreshTokenHistoryPruned"),
        "Removed {RefreshTokenCount} expired refresh-token history records.");
    private static readonly Action<ILogger, Exception?> s_pruningFailed = LoggerMessage.Define(
        LogLevel.Warning, new EventId(1002, "RefreshTokenPruningFailed"),
        "Unable to prune expired refresh-token history; will retry.");

    private readonly RefreshTokenCleanupOptions _options = options.Value;

    protected async override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using PeriodicTimer timer = new(_options.Interval, timeProvider);
        try
        {
            do
            {
                await PruneOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Application shutdown cancels both the query and the periodic timer.
        }
    }

    private async Task PruneOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IdentityDbContext context = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            DateTimeOffset cutoff = timeProvider.GetUtcNow().Subtract(_options.Retention);
            int deleted = await SelectExpiredHistory(context.Set<RefreshToken>(), cutoff).ExecuteDeleteAsync(cancellationToken);
            if (deleted > 0)
            {
                s_historyPruned(logger, deleted, null);
            }
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Database outages should not take down the API; the next tick retries cleanup.
            s_pruningFailed(logger, exception);
        }
    }

    internal static IQueryable<RefreshToken> SelectExpiredHistory(IQueryable<RefreshToken> tokens, DateTimeOffset cutoff)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        // Never remove an unexpired token, even if it has already been consumed.
        // Old tokens in active families are retained until their own expiry plus retention.
        return tokens.Where(token => token.ExpiresAt < cutoff);
    }
}
