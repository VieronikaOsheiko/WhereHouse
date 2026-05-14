using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Application;
using Warehouse.Application.Dtos;
using Warehouse.Domain;
using Warehouse.Infrastructure;
using Warehouse.Infrastructure.Persistence;
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
    public async Task After_reset_zones_table_is_empty()
    {
        await _fixture.ResetDatabaseAsync();
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();

        (await db.Zones.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Create_zone_via_service_is_persisted()
    {
        await _fixture.ResetDatabaseAsync();
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<WarehouseService>();

        await svc.CreateZoneAsync(new CreateZoneDto("ExplainZone", "Shipping", "Ambient"), CancellationToken.None);

        (await db.Zones.CountAsync()).Should().Be(1);
        (await db.Zones.AsNoTracking().SingleAsync()).Name.Should().Be("ExplainZone");
    }

    [Fact]
    public async Task Place_item_increases_shelf_current_load()
    {
        await _fixture.ResetDatabaseAsync();
        await using var provider = BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        var svc = scope.ServiceProvider.GetRequiredService<WarehouseService>();

        var zone = new Zone { Id = Guid.NewGuid(), Name = "EZ", Type = ZoneType.Storage, Temperature = Temperature.Ambient };
        var shelf = new Shelf { Id = Guid.NewGuid(), ZoneId = zone.Id, Code = "E1", Capacity = 100m, CurrentLoad = 0m };
        db.Zones.Add(zone);
        db.Shelves.Add(shelf);
        await db.SaveChangesAsync();

        await svc.PlaceItemAsync(new PlaceItemDto(
            "ExplainItem",
            $"SKU-E-{Guid.NewGuid():N}",
            2.5m,
            "Ambient",
            shelf.Id,
            4,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))), CancellationToken.None);

        var load = (await db.Shelves.AsNoTracking().FirstAsync(s => s.Id == shelf.Id)).CurrentLoad;
        load.Should().Be(10m);
    }
}
