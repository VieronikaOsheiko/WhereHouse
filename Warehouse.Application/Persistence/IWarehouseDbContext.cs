using Microsoft.EntityFrameworkCore;
using Warehouse.Domain;

namespace Warehouse.Application.Persistence;

public interface IWarehouseDbContext
{
    DbSet<Zone> Zones { get; }
    DbSet<Shelf> Shelves { get; }
    DbSet<Item> Items { get; }
    DbSet<Order> Orders { get; }
    DbSet<OrderLine> OrderLines { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
