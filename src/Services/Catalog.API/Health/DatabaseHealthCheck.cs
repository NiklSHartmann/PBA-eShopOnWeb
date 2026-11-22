using Catalog.API.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Catalog.API.Health;

/// <summary>
/// /health svarer kun "Healthy", hvis katalogtabellen kan læses. Det er ikke nok, at SQL Server svarer:
/// catalogapi-2 starter, før catalogapi-1 har kørt migrations, og den må først få kald, når tabellerne findes.
/// Gatewayen spørger hver instans på /health og sender kun kald til dem, der svarer "Healthy".
/// </summary>
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly CatalogDbContext _db;

    public DatabaseHealthCheck(CatalogDbContext db)
    {
        _db = db;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await _db.CatalogItems.AnyAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("The catalog database cannot be read yet", ex);
        }
    }
}
