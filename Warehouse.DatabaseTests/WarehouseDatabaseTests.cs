using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Application;
using Warehouse.Application.Dtos;
using Warehouse.Domain;
using Warehouse.Infrastructure;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Tests.Common;
using Xunit;

namespace Warehouse.DatabaseTests;

public class WarehouseDatabaseTests : IClassFixture<PostgresFixture>
{
    private readonly PostgresFixture _fixture;

    public WarehouseDatabaseTests(PostgresFixture fixture) => _fixture = fixture;

    private ServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddWarehouseInfrastructure(_fixture.ConnectionString);
        services.AddScoped<WarehouseService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Shelf_capacity_is_enforced_by_weight()
    {
        await _fixture.ResetDatabaseAsync();
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<WarehouseService>();

        var zone = new Zone { Id = Guid.NewGuid(), Name = "Z1", Type = ZoneType.Storage, Temperature = Temperature.Ambient };
        var shelf = new Shelf { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = "S1", Capacity = 10m, CurrentLoad = 9m };
        db.Zones.Add(zone);
        db.Shelves.Add(shelf);
        await db.SaveChangesAsync();

        var act = async () => await svc.PlaceItemAsync(new PlaceItemDto(
            "Box",
            "SKU-SINGLE",
            2m,
            "Ambient",
            shelf.Id,
            1,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10))), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Temperature_must_match_zone_when_placing()
    {
        await _fixture.ResetDatabaseAsync();
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<WarehouseService>();

        var zone = new Zone { Id = Guid.NewGuid(), Name = "ColdZ", Type = ZoneType.Storage, Temperature = Temperature.Cold };
        var shelf = new Shelf { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = "C1", Capacity = 1000m, CurrentLoad = 0 };
        db.Zones.Add(zone);
        db.Shelves.Add(shelf);
        await db.SaveChangesAsync();

        var act = async () => await svc.PlaceItemAsync(new PlaceItemDto(
            "Fish",
            "SKU-FISH",
            1m,
            "Frozen",
            shelf.Id,
            2,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(5))), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Shipping_reduces_inventory_and_shelf_load()
    {
        await _fixture.ResetDatabaseAsync();
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<WarehouseService>();

        var zone = new Zone { Id = Guid.NewGuid(), Name = "Ship", Type = ZoneType.Picking, Temperature = Temperature.Ambient };
        var shelf = new Shelf { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = "P1", Capacity = 500m, CurrentLoad = 0 };
        db.Zones.Add(zone);
        db.Shelves.Add(shelf);
        await db.SaveChangesAsync();

        var item = await svc.PlaceItemAsync(new PlaceItemDto(
            "Goods",
            "SKU-GOODS",
            2m,
            "Ambient",
            shelf.Id,
            10,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20))), CancellationToken.None);

        var order = await svc.CreateOrderAsync(new CreateOrderDto([
            new OrderLineRequestDto(item.Id, 10)
        ]), CancellationToken.None);

        var loadBefore = (await db.Shelves.AsNoTracking().FirstAsync(s => s.Id == shelf.Id)).CurrentLoad;

        await svc.PatchOrderStatusAsync(order.Id, new PatchOrderStatusDto("Shipped"), CancellationToken.None);

        var shelfAfter = await db.Shelves.AsNoTracking().FirstAsync(s => s.Id == shelf.Id);
        shelfAfter.CurrentLoad.Should().Be(loadBefore - 20m);

        var exists = await db.Items.AnyAsync(i => i.Id == item.Id);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task Large_seed_meets_minimum_row_count()
    {
        await _fixture.ResetDatabaseAsync();
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();

        await LargeDatasetSeeder.SeedAsync(db);

        var zones = await db.Zones.CountAsync();
        var shelves = await db.Shelves.CountAsync();
        var items = await db.Items.CountAsync();
        var orders = await db.Orders.CountAsync();
        var lines = await db.OrderLines.CountAsync();

        (zones + shelves + items + orders + lines).Should().BeGreaterThanOrEqualTo(LargeDatasetSeeder.MinimumTotalRows);
    }

    [Fact]
    public async Task Concurrent_shipments_do_not_oversell_item()
    {
        await _fixture.ResetDatabaseAsync();
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<WarehouseService>();

        var zone = new Zone { Id = Guid.NewGuid(), Name = "Race", Type = ZoneType.Storage, Temperature = Temperature.Ambient };
        var shelf = new Shelf { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = "R1", Capacity = 1000m, CurrentLoad = 0 };
        db.Zones.Add(zone);
        db.Shelves.Add(shelf);
        await db.SaveChangesAsync();

        var inv = await svc.PlaceItemAsync(new PlaceItemDto(
            "Limited",
            "SKU-RACE",
            1m,
            "Ambient",
            shelf.Id,
            3,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(15))), CancellationToken.None);

        var o1 = await svc.CreateOrderAsync(new CreateOrderDto([new OrderLineRequestDto(inv.Id, 2)]), CancellationToken.None);
        var o2 = await svc.CreateOrderAsync(new CreateOrderDto([new OrderLineRequestDto(inv.Id, 2)]), CancellationToken.None);

        await svc.PatchOrderStatusAsync(o1.Id, new PatchOrderStatusDto("Shipped"), CancellationToken.None);

        var act = async () =>
            await svc.PatchOrderStatusAsync(o2.Id, new PatchOrderStatusDto("Shipped"), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
