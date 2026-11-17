using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Portfolio.Web.Data;

/// <summary>
/// Applies migrations and creates the single admin account on startup.
/// If the database is unreachable the error is logged and the public site keeps running
/// (pages fall back to built-in content), rather than the whole app failing to start.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services, IConfiguration config, ILogger logger)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        try
        {
            if (config.GetValue("Database:MigrateOnStartup", true))
            {
                var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
                if (pending.Count > 0)
                {
                    logger.LogInformation("Applying {Count} database migration(s): {Migrations}", pending.Count, pending);
                    await db.Database.MigrateAsync();
                }
            }

            await EnsureAdminAsync(scope.ServiceProvider, config, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database initialisation failed. The site will run with built-in content until the database is reachable.");
        }
    }

    private static async Task EnsureAdminAsync(IServiceProvider sp, IConfiguration config, ILogger logger)
    {
        var users = sp.GetRequiredService<UserManager<IdentityUser>>();
        if (await users.Users.AnyAsync())
            return;

        var email = config["Admin:Email"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No admin account exists yet. Set Admin:Email and Admin:Password (user secrets or environment variables) to create one.");
            return;
        }

        var admin = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await users.CreateAsync(admin, password);
        if (result.Succeeded)
            logger.LogInformation("Created the admin account. Two-factor sign-in will be set up on first login.");
        else
            logger.LogError("Could not create the admin account: {Errors}", string.Join("; ", result.Errors.Select(e => e.Description)));
    }
}
