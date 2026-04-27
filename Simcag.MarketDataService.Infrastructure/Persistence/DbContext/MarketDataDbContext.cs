using Microsoft.EntityFrameworkCore;
using Simcag.MarketDataService.Domain.Entities;

namespace Simcag.MarketDataService.Infrastructure.Persistence.DbContext;

public class MarketDataDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public MarketDataDbContext(DbContextOptions<MarketDataDbContext> options)
        : base(options)
    {
    }

    public DbSet<MarketPrice> MarketPrices => Set<MarketPrice>();
    public DbSet<MarketPriceHistory> MarketPriceHistory => Set<MarketPriceHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MarketPrice>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.Source)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CollectedDate)
                .IsRequired();

            entity.Property(e => e.IsActive)
                .IsRequired();

            entity.Property(e => e.ExpenseCategory)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(e => e.GeographicRegion)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(e => e.SupplierProfile)
                .IsRequired()
                .HasMaxLength(120);

            entity.HasIndex(e => e.ProductName);
            entity.HasIndex(e => new { e.ProductName, e.IsActive });
            entity.HasIndex(e => new { e.ExpenseCategory, e.GeographicRegion, e.IsActive });
        });

        modelBuilder.Entity<MarketPriceHistory>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ProductName)
                .IsRequired()
                .HasMaxLength(500);

            entity.Property(e => e.Price)
                .HasColumnType("decimal(18,2)")
                .IsRequired();

            entity.Property(e => e.Source)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CollectedDate)
                .IsRequired();

            entity.HasIndex(e => e.ProductName);
            entity.HasIndex(e => e.CollectedDate);
        });
    }
}
