using Bogus;
using Microsoft.EntityFrameworkCore;
using Warehouse.Domain;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Tests.Common;

/// <summary>
/// Generates >= 10k rows across zones, shelves, items, and orders with realistic parent/child ratios.
/// Uses Bogus for non-critical text/timestamps; business fields are explicit.
/// </summary>
public static class LargeDatasetSeeder
{
    public const int MinimumTotalRows = 10_000;

    public static async Task SeedAsync(WarehouseDbContext db, CancellationToken ct = default)
    {
        if (await db.Zones.AnyAsync(ct))
            return;

        var faker = new Faker();
        var random = new Random(42);

        const int zoneCount = 20;
        const int shelvesPerZone = 100;

        var zones = new List<Zone>();
        for (var z = 0; z < zoneCount; z++)
        {
            zones.Add(new Zone
            {
                Id = Guid.NewGuid(),
                Name = faker.Commerce.Department(),
                Type = (ZoneType)(1 + z % 3),
                Temperature = (Temperature)(1 + z % 3)
            });
        }

        var shelves = new List<Shelf>();
        foreach (var zone in zones)
        {
            for (var s = 0; s < shelvesPerZone; s++)
            {
                var cap = 8000m + random.Next(0, 8000);
                shelves.Add(new Shelf
                {
                    Id = Guid.NewGuid(),
                    ZoneId = zone.Id,
                    Code = $"{zone.Type.ToString()[0]}-{faker.Random.AlphaNumeric(8).ToUpperInvariant()}-{s:D3}",
                    Capacity = cap,
                    CurrentLoad = 0
                });
            }
        }

        var items = new List<Item>();
        var skuCounter = 1;
        foreach (var shelf in shelves)
        {
            var zone = zones.First(x => x.Id == shelf.ZoneId);
            var linesOnShelf = 2 + random.Next(0, 6);
            for (var i = 0; i < linesOnShelf; i++)
            {
                var qty = 1 + random.Next(0, 20);
                var weight = 0.25m + (decimal)(random.NextDouble() * 25);
                var load = weight * qty;
                if (shelf.CurrentLoad + load > shelf.Capacity)
                    continue;

                items.Add(new Item
                {
                    Id = Guid.NewGuid(),
                    Name = faker.Commerce.ProductName(),
                    Sku = $"SKU-{skuCounter++:D7}",
                    Weight = decimal.Round(weight, 4),
                    RequiredTemperature = zone.Temperature,
                    ShelfId = shelf.Id,
                    Quantity = qty,
                    ExpiryDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(random.Next(7, 500)))
                });
                shelf.CurrentLoad += load;
            }
        }

        var orders = new List<Order>();
        var orderLines = new List<OrderLine>();
        const int orderCount = 600;
        for (var o = 0; o < orderCount; o++)
        {
            var order = new Order
            {
                Id = Guid.NewGuid(),
                Status = OrderStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-random.Next(0, 90)),
                ShippedAt = null
            };
            orders.Add(order);

            var lineCount = 1 + random.Next(0, 4);
            foreach (var inv in items.OrderBy(_ => random.Next()).Take(lineCount))
            {
                var q = Math.Min(1 + random.Next(0, 2), Math.Max(1, inv.Quantity / 2));
                orderLines.Add(new OrderLine
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    ItemId = inv.Id,
                    Quantity = q
                });
            }
        }

        db.Zones.AddRange(zones);
        db.Shelves.AddRange(shelves);
        db.Items.AddRange(items);
        db.Orders.AddRange(orders);
        db.OrderLines.AddRange(orderLines);
        await db.SaveChangesAsync(ct);

        var total = zones.Count + shelves.Count + items.Count + orders.Count + orderLines.Count;
        if (total < MinimumTotalRows)
            throw new InvalidOperationException($"Seed produced only {total} rows; expected at least {MinimumTotalRows}.");
    }
}
