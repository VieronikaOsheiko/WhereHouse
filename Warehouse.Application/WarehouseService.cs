using Microsoft.EntityFrameworkCore;
using Warehouse.Application.Dtos;
using Warehouse.Application.Parsing;
using Warehouse.Application.Persistence;
using Warehouse.Domain;

namespace Warehouse.Application;

public class WarehouseService(IWarehouseDbContext db)
{
    public async Task<IReadOnlyList<ZoneResponseDto>> GetZonesAsync(CancellationToken ct = default)
    {
        var zones = await db.Zones
            .AsNoTracking()
            .Include(z => z.Shelves)
            .ToListAsync(ct);

        return zones.Select(z =>
        {
            var shelves = z.Shelves.ToList();
            var totalCap = shelves.Sum(s => s.Capacity);
            var totalLoad = shelves.Sum(s => s.CurrentLoad);
            var ratio = totalCap > 0 ? totalLoad / totalCap : 0m;
            return new ZoneResponseDto(
                z.Id,
                z.Name,
                z.Type.ToString(),
                z.Temperature.ToString(),
                ratio);
        }).ToList();
    }

    public async Task<ZoneResponseDto> CreateZoneAsync(CreateZoneDto dto, CancellationToken ct = default)
    {
        if (!EnumParser.TryParseZoneType(dto.Type, out var zoneType))
            throw new ArgumentException("Invalid zone type.", nameof(dto));
        if (!EnumParser.TryParseTemperature(dto.Temperature, out var temp))
            throw new ArgumentException("Invalid temperature.", nameof(dto));

        var zone = new Zone
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Type = zoneType,
            Temperature = temp
        };
        db.Zones.Add(zone);
        await db.SaveChangesAsync(ct);

        return new ZoneResponseDto(zone.Id, zone.Name, zone.Type.ToString(), zone.Temperature.ToString(), 0m);
    }

    public async Task<IReadOnlyList<ShelfResponseDto>> GetShelvesAsync(
        Guid? zoneId,
        decimal? minAvailableCapacity,
        CancellationToken ct = default)
    {
        var query = db.Shelves.AsNoTracking().AsQueryable();
        if (zoneId is { } zid)
            query = query.Where(s => s.ZoneId == zid);

        var list = await query.OrderBy(s => s.Code).ToListAsync(ct);

        IEnumerable<ShelfResponseDto> projected = list.Select(s =>
        {
            var avail = s.Capacity - s.CurrentLoad;
            return new ShelfResponseDto(s.Id, s.ZoneId, s.Code, s.Capacity, s.CurrentLoad, avail);
        });

        if (minAvailableCapacity is { } min)
            projected = projected.Where(s => s.AvailableCapacity >= min);

        return projected.ToList();
    }

    public async Task<ItemResponseDto> PlaceItemAsync(PlaceItemDto dto, CancellationToken ct = default)
    {
        if (!EnumParser.TryParseTemperature(dto.RequiredTemperature, out var reqTemp))
            throw new ArgumentException("Invalid required temperature.", nameof(dto));

        if (await db.Items.AnyAsync(i => i.Sku == dto.Sku, ct))
            throw new InvalidOperationException("SKU must be unique.");

        var shelf = await db.Shelves
            .Include(s => s.Zone)
            .FirstOrDefaultAsync(s => s.Id == dto.ShelfId, ct)
            ?? throw new KeyNotFoundException("Shelf not found.");

        if (!WarehouseRules.TemperatureMatchesZone(reqTemp, shelf.Zone.Temperature))
            throw new InvalidOperationException("Item required temperature must match zone temperature.");

        var additional = WarehouseRules.ComputeShelfLoad(dto.Weight, dto.Quantity);
        if (!WarehouseRules.FitsCapacity(shelf.CurrentLoad, additional, shelf.Capacity))
            throw new InvalidOperationException("Shelf would exceed capacity.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (WarehouseRules.IsExpired(dto.ExpiryDate, today))
            throw new InvalidOperationException("Cannot place expired product.");

        var item = new Item
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Sku = dto.Sku,
            Weight = dto.Weight,
            RequiredTemperature = reqTemp,
            ShelfId = shelf.Id,
            Quantity = dto.Quantity,
            ExpiryDate = dto.ExpiryDate
        };
        shelf.CurrentLoad += additional;
        db.Items.Add(item);
        await db.SaveChangesAsync(ct);

        return ToItemDto(item);
    }

    public async Task<ItemResponseDto> MoveItemAsync(Guid itemId, MoveItemDto dto, CancellationToken ct = default)
    {
        var item = await db.Items
            .Include(i => i.Shelf)
            .ThenInclude(s => s.Zone)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct)
            ?? throw new KeyNotFoundException("Item not found.");

        var target = await db.Shelves
            .Include(s => s.Zone)
            .FirstOrDefaultAsync(s => s.Id == dto.TargetShelfId, ct)
            ?? throw new KeyNotFoundException("Target shelf not found.");

        if (item.ShelfId == target.Id)
            return ToItemDto(item);

        if (!WarehouseRules.TemperatureMatchesZone(item.RequiredTemperature, target.Zone.Temperature))
            throw new InvalidOperationException("Item required temperature must match target zone temperature.");

        var load = WarehouseRules.ComputeShelfLoad(item.Weight, item.Quantity);
        if (!WarehouseRules.FitsCapacity(target.CurrentLoad, load, target.Capacity))
            throw new InvalidOperationException("Target shelf would exceed capacity.");

        item.Shelf!.CurrentLoad -= load;
        target.CurrentLoad += load;
        item.ShelfId = target.Id;
        await db.SaveChangesAsync(ct);

        return ToItemDto(item);
    }

    public async Task<IReadOnlyList<ItemResponseDto>> GetExpiringItemsAsync(int days, CancellationToken ct = default)
    {
        if (days < 0)
            throw new ArgumentOutOfRangeException(nameof(days));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var until = today.AddDays(days);

        var items = await db.Items
            .AsNoTracking()
            .Where(i => i.ExpiryDate >= today && i.ExpiryDate <= until)
            .OrderBy(i => i.ExpiryDate)
            .ToListAsync(ct);

        return items.Select(ToItemDto).ToList();
    }

    public async Task<OrderResponseDto> CreateOrderAsync(CreateOrderDto dto, CancellationToken ct = default)
    {
        if (dto.Lines.Count == 0)
            throw new ArgumentException("Order must have at least one line.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var aggregatedLines = dto.Lines
            .GroupBy(l => l.ItemId)
            .Select(g => (ItemId: g.Key, Quantity: g.Sum(x => x.Quantity)))
            .ToList();

        var order = new Order
        {
            Id = Guid.NewGuid(),
            Status = OrderStatus.Pending,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Orders.Add(order);

        foreach (var line in aggregatedLines)
        {
            var item = await db.Items
                .Include(i => i.Shelf)
                .ThenInclude(s => s.Zone)
                .FirstOrDefaultAsync(i => i.Id == line.ItemId, ct)
                ?? throw new KeyNotFoundException($"Item {line.ItemId} not found.");

            if (WarehouseRules.IsExpired(item.ExpiryDate, today))
                throw new InvalidOperationException($"Expired item cannot be included: {item.Id}.");

            if (line.Quantity <= 0)
                throw new ArgumentException("Quantity must be positive.");

            if (item.Quantity < line.Quantity)
                throw new InvalidOperationException($"Insufficient quantity for item {item.Id}.");

            db.OrderLines.Add(new OrderLine
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                ItemId = item.Id,
                Quantity = line.Quantity
            });
        }

        await db.SaveChangesAsync(ct);

        return await GetOrderAsync(order.Id, ct);
    }

    public async Task<OrderResponseDto> PatchOrderStatusAsync(Guid orderId, PatchOrderStatusDto dto, CancellationToken ct = default)
    {
        if (!EnumParser.TryParseOrderStatus(dto.Status, out var newStatus))
            throw new ArgumentException("Invalid order status.", nameof(dto));

        var order = await db.Orders
            .Include(o => o.Lines)
            .ThenInclude(l => l.Item!)
            .ThenInclude(i => i.Shelf)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct)
            ?? throw new KeyNotFoundException("Order not found.");

        if (order.Status == OrderStatus.Shipped)
            throw new InvalidOperationException("Cannot change status of a shipped order.");

        if (newStatus == OrderStatus.Shipped)
        {
            await FulfillShipmentAsync(order, ct);
            order.ShippedAt = DateTimeOffset.UtcNow;
        }

        order.Status = newStatus;
        await db.SaveChangesAsync(ct);

        return await GetOrderAsync(order.Id, ct);
    }

    private async Task FulfillShipmentAsync(Order order, CancellationToken ct)
    {
        foreach (var line in order.Lines)
        {
            if (line.ItemId is null)
                continue;

            var item = line.Item;
            if (item is null)
            {
                item = await db.Items
                    .Include(i => i.Shelf)
                    .FirstAsync(i => i.Id == line.ItemId, ct);
                line.Item = item;
            }

            if (line.Quantity > item.Quantity)
                throw new InvalidOperationException($"Insufficient stock for item {item.Id} at shipment.");

            var load = WarehouseRules.ComputeShelfLoad(item.Weight, line.Quantity);
            item.Shelf!.CurrentLoad -= load;
            item.Quantity -= line.Quantity;

            if (item.Quantity == 0)
            {
                line.ItemId = null;
                line.Item = null;
                db.Items.Remove(item);
            }
        }
    }

    private async Task<OrderResponseDto> GetOrderAsync(Guid orderId, CancellationToken ct)
    {
        var order = await db.Orders
            .AsNoTracking()
            .Include(o => o.Lines)
            .FirstAsync(o => o.Id == orderId, ct);

        var lines = order.Lines
            .Select(l => new OrderLineResponseDto(l.ItemId, l.Quantity))
            .ToList();
        return new OrderResponseDto(
            order.Id,
            order.Status.ToString(),
            order.CreatedAt,
            order.ShippedAt,
            lines);
    }

    private static ItemResponseDto ToItemDto(Item item) =>
        new(
            item.Id,
            item.Name,
            item.Sku,
            item.Weight,
            item.RequiredTemperature.ToString(),
            item.ShelfId,
            item.Quantity,
            item.ExpiryDate);
}
