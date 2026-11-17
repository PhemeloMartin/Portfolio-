using Microsoft.EntityFrameworkCore;
using Portfolio.Web.Data;

namespace Portfolio.Web.Services;

/// <summary>
/// Deletes contact messages older than the retention period (POPIA: don't keep personal data longer than needed).
/// Runs shortly after startup and then once a day.
/// </summary>
public sealed class MessageRetentionService(
    IServiceScopeFactory scopes, IConfiguration config, ILogger<MessageRetentionService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
            do
            {
                await PurgeAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // App is shutting down.
        }
    }

    private async Task PurgeAsync(CancellationToken ct)
    {
        var days = Math.Max(1, config.GetValue("Privacy:MessageRetentionDays", 180));
        var cutoff = DateTime.UtcNow.AddDays(-days);
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var deleted = await db.ContactMessages.Where(m => m.CreatedAtUtc < cutoff).ExecuteDeleteAsync(ct);
            if (deleted > 0)
                logger.LogInformation("Deleted {Count} contact message(s) older than {Days} days", deleted, days);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Message retention clean-up failed; will retry tomorrow");
        }
    }
}
