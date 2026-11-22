using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Data;

/// <summary>
/// Kopierer kataloget fra monolitten én gang, mens Catalog.API's database stadig er tom.
/// Mærker, typer og varer beholder deres id'er, fordi kurve, ordrer og lageret allerede bruger dem.
/// Alt kopieres i én transaktion: enten kommer hele kataloget over, eller intet gør.
/// </summary>
public class MonolithCatalogImporter
{
    private readonly CatalogDbContext _db;
    private readonly IMonolithCatalogSource _source;
    private readonly ILogger<MonolithCatalogImporter> _logger;

    public MonolithCatalogImporter(CatalogDbContext db, IMonolithCatalogSource source, ILogger<MonolithCatalogImporter> logger)
    {
        _db = db;
        _source = source;
        _logger = logger;
    }

    public async Task ImportIfEmptyAsync(CancellationToken cancellationToken)
    {
        if (await _db.CatalogBrands.AnyAsync(cancellationToken) || await _db.CatalogItems.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Catalog database already has data; nothing copied from the monolith");
            return;
        }

        var snapshot = await _source.ReadAsync(cancellationToken);
        if (snapshot.Items.Count == 0)
        {
            // Web seeder monolittens katalog ved opstart. Er det ikke sket endnu, prøver initializeren igen.
            throw new InvalidOperationException("The monolith catalog has no items yet, so there is nothing to copy");
        }

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        await InsertKeepingIdsAsync(snapshot.Brands, "CatalogBrands", cancellationToken);
        await InsertKeepingIdsAsync(snapshot.Types, "CatalogTypes", cancellationToken);
        await InsertKeepingIdsAsync(snapshot.Items, "CatalogItems", cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        _logger.LogInformation("Copied {BrandCount} brands, {TypeCount} types and {ItemCount} items from the monolith catalog",
            snapshot.Brands.Count, snapshot.Types.Count, snapshot.Items.Count);
    }

    private async Task InsertKeepingIdsAsync<T>(IReadOnlyList<T> rows, string table, CancellationToken cancellationToken)
        where T : class
    {
        _db.Set<T>().AddRange(rows);

        // SQL Server vil kun tage imod et id, vi selv vælger, når IDENTITY_INSERT er slået til for tabellen.
        // Det gælder kun denne forbindelse, og transaktionen holder forbindelsen åben. SQLite (testene) har ikke brug for det.
        var isSqlServer = _db.Database.IsSqlServer();
        if (isSqlServer)
        {
            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo." + table + " ON", cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        if (isSqlServer)
        {
            await _db.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT dbo." + table + " OFF", cancellationToken);
        }
    }
}
