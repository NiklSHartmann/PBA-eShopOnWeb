using Catalog.API.Domain;
using Microsoft.Data.SqlClient;

namespace Catalog.API.Data;

/// <summary>
/// Læser kataloget direkte fra monolittens tabeller (Catalog, CatalogBrands, CatalogTypes).
/// Det er den eneste gang, Catalog.API rører monolittens database: én kopi, før trafikken flyttes.
/// Forbindelsen står i ConnectionStrings:MonolithCatalog og bruges kun til at læse.
/// </summary>
public class SqlMonolithCatalogSource : IMonolithCatalogSource
{
    private readonly string _connectionString;

    public SqlMonolithCatalogSource(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MonolithCatalog")
            ?? throw new InvalidOperationException("ConnectionStrings:MonolithCatalog is missing from the configuration");
    }

    public async Task<MonolithCatalogSnapshot> ReadAsync(CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        var brands = await QueryAsync(connection, "SELECT Id, Brand FROM dbo.CatalogBrands",
            row => CatalogBrand.CopyFromMonolith(row.GetInt32(0), row.GetString(1)), cancellationToken);

        var types = await QueryAsync(connection, "SELECT Id, Type FROM dbo.CatalogTypes",
            row => CatalogType.CopyFromMonolith(row.GetInt32(0), row.GetString(1)), cancellationToken);

        var items = await QueryAsync(connection,
            "SELECT Id, Name, Description, Price, PictureUri, CatalogTypeId, CatalogBrandId FROM dbo.Catalog",
            row => CatalogItem.CopyFromMonolith(
                id: row.GetInt32(0),
                catalogTypeId: row.GetInt32(5),
                catalogBrandId: row.GetInt32(6),
                description: row.IsDBNull(2) ? string.Empty : row.GetString(2),
                name: row.GetString(1),
                price: row.GetDecimal(3),
                pictureUri: row.IsDBNull(4) ? string.Empty : row.GetString(4)),
            cancellationToken);

        return new MonolithCatalogSnapshot(brands, types, items);
    }

    private static async Task<List<T>> QueryAsync<T>(SqlConnection connection, string sql,
        Func<SqlDataReader, T> map, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var rows = new List<T>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(map(reader));
        }

        return rows;
    }
}
