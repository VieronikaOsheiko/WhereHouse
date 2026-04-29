using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Persistence;
using Warehouse.Domain;

namespace Warehouse.Infrastructure.Persistence;

public sealed class WarehouseDbContext(DbContextOptions<WarehouseDbContext> options)
    : DbContext(options), IWarehouseDbContext
{
    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Shelf> Shelves => Set<Shelf>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Zone>(e =>
        {
            e.HasKey(z => z.Id);
            e.Property(z => z.Name).HasMaxLength(256).IsRequired();
            e.HasMany(z => z.Shelves)
                .WithOne(s => s.Zone)
                .HasForeignKey(s => s.ZoneId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Shelf>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Code).HasMaxLength(64).IsRequired();
            e.HasIndex(s => new { s.ZoneId, s.Code }).IsUnique();
            e.Property(s => s.Capacity).HasPrecision(18, 4);
            e.Property(s => s.CurrentLoad).HasPrecision(18, 4);
        });

        modelBuilder.Entity<Item>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Name).HasMaxLength(512).IsRequired();
            e.Property(i => i.Sku).HasMaxLength(128).IsRequired();
            e.HasIndex(i => i.Sku).IsUnique();
            e.Property(i => i.Weight).HasPrecision(18, 4);
            e.HasOne(i => i.Shelf)
                .WithMany(s => s.Items)
                .HasForeignKey(i => i.ShelfId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Order>(e =>
        {
            e.HasKey(o => o.Id);
            e.HasMany(o => o.Lines)
                .WithOne(l => l.Order)
                .HasForeignKey(l => l.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderLine>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasOne(l => l.Item)
                .WithMany()
                .HasForeignKey(l => l.ItemId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
