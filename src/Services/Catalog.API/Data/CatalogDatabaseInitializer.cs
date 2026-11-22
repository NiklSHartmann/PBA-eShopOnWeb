using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Data;

/// <summary>
/// Gør databasen klar ved opstart: migrations og den ene kopi fra monolitten.
/// Kun én instans må gøre det (Catalog:InitializeDatabase=true på catalogapi-1); ellers ville to instanser
/// migrere og kopiere samtidig. Den anden instans venter bare: dens /health svarer "Unhealthy", indtil
/// tabellerne findes, så gatewayen sender ingen kald til den imens.
/// SQL Server og monolittens seed er ikke nødvendigvis klar, når containeren starter, så der prøves igen et stykke tid.
/// </summary>
public static class CatalogDatabaseInitializer
{
    private const int MaxAttempts = 20;
    private static readonly TimeSpan DelayBetweenAttempts = TimeSpan.FromSeconds(3);

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration, ILogger logger,
        CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("Catalog:InitializeDatabase"))
        {
            logger.LogInformation("Catalog database setup skipped on this instance (Catalog:InitializeDatabase is false)");
            return;
        }

        using (var scope = services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            if (!db.Database.GetMigrations().Any())
            {
                throw new InvalidOperationException("Catalog.API has no migrations. Create them with: dotnet ef migrations add InitialCreate --project src/Services/Catalog.API --startup-project src/Services/Catalog.API --output-dir Data/Migrations");
            }
        }

        var copyFromMonolith = !string.IsNullOrWhiteSpace(configuration.GetConnectionString("MonolithCatalog"));

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var scope = services.CreateScope();
                await MigrateAsync(scope.ServiceProvider.GetRequiredService<CatalogDbContext>(), logger, cancellationToken);

                if (copyFromMonolith)
                {
                    await scope.ServiceProvider.GetRequiredService<MonolithCatalogImporter>().ImportIfEmptyAsync(cancellationToken);
                }
                else
                {
                    logger.LogWarning("ConnectionStrings:MonolithCatalog is not set; nothing is copied from the monolith");
                }

                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts)
            {
                logger.LogWarning(ex, "Catalog database is not ready (attempt {Attempt} of {MaxAttempts}); trying again in {DelaySeconds} s",
                    attempt, MaxAttempts, DelayBetweenAttempts.TotalSeconds);
                await Task.Delay(DelayBetweenAttempts, cancellationToken);
            }
        }
    }

    // Tre udfald logges forskelligt, ligesom i Inventory.API: allerede opdateret, opdateret nu, eller fejl (kastes videre).
    private static async Task MigrateAsync(CatalogDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        var pendingMigrations = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pendingMigrations.Count == 0)
        {
            logger.LogInformation("Catalog database is up to date");
            return;
        }

        await db.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Applied {MigrationCount} migrations to the catalog database: {Migrations}",
            pendingMigrations.Count, pendingMigrations);
    }
}
