using Catalog.API.Domain;
using Microsoft.EntityFrameworkCore;

namespace Catalog.API.Data;

/// <summary>
/// Catalog.API's egen database. Den ligger i SQL Server og ikke i en SQLite-fil som de andre services,
/// fordi to instanser af Catalog.API skal dele den (load balancing i gatewayen).
/// Id'erne er almindelige IDENTITY-kolonner; ved kopieringen fra monolitten indsættes de gamle id'er.
/// </summary>
public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options)
    {
    }

    public DbSet<CatalogItem> CatalogItems => Set<CatalogItem>();
    public DbSet<CatalogBrand> CatalogBrands => Set<CatalogBrand>();
    public DbSet<CatalogType> CatalogTypes => Set<CatalogType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CatalogBrand>(brand =>
        {
            brand.HasKey(b => b.Id);
            brand.Property(b => b.Brand).IsRequired().HasMaxLength(100);
        });

        modelBuilder.Entity<CatalogType>(type =>
        {
            type.HasKey(t => t.Id);
            type.Property(t => t.Type).IsRequired().HasMaxLength(100);
        });

        // Samme længder og decimaltype som monolittens CatalogItemConfiguration.
        modelBuilder.Entity<CatalogItem>(item =>
        {
            item.HasKey(i => i.Id);
            item.Property(i => i.Name).IsRequired().HasMaxLength(50);
            item.Property(i => i.Description).IsRequired();
            item.Property(i => i.Price).HasColumnType("decimal(18,2)");
            item.Property(i => i.PictureUri).IsRequired();

            // Fremmednøgler uden navigation: en vare kan ikke pege på et mærke eller en type, der ikke findes.
            item.HasOne<CatalogBrand>().WithMany().HasForeignKey(i => i.CatalogBrandId);
            item.HasOne<CatalogType>().WithMany().HasForeignKey(i => i.CatalogTypeId);
        });
    }
}
