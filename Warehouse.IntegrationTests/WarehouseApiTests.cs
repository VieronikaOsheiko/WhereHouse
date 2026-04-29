using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Warehouse.Application.Dtos;
using Xunit;

namespace Warehouse.IntegrationTests;

public class WarehouseApiTests(WarehouseWebApplicationFactory factory) : IClassFixture<WarehouseWebApplicationFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Get_zones_returns_ok_and_fill_ratio()
    {
        var res = await _client.GetAsync("/api/zones");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await res.Content.ReadFromJsonAsync<List<ZoneRow>>();
        json.Should().NotBeNull();
        json!.Count.Should().BeGreaterThan(0);
        json.Should().OnlyContain(z => z.OccupancyRatio >= 0 && z.OccupancyRatio <= 1);
    }

    [Fact]
    public async Task Place_item_rejects_temperature_mismatch()
    {
        var zones = (await _client.GetFromJsonAsync<List<ZoneRow>>("/api/zones"))!;
        var ambient = zones.First(z => z.Temperature == "Ambient");
        var shelfId = (await _client.GetFromJsonAsync<List<ShelfRow>>($"/api/shelves?zoneId={ambient.Id}"))!.First().Id;

        var bad = await _client.PostAsJsonAsync("/api/items", new PlaceItemDto(
            "Ice",
            $"SKU-{Guid.NewGuid():N}",
            1m,
            "Frozen",
            shelfId,
            1,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))));

        bad.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_order_rejects_expired_inventory()
    {
        var zones = (await _client.GetFromJsonAsync<List<ZoneRow>>("/api/zones"))!;
        var cold = zones.First(z => z.Temperature == "Cold");
        var shelfId = (await _client.GetFromJsonAsync<List<ShelfRow>>($"/api/shelves?zoneId={cold.Id}"))!.First().Id;

        var place = await _client.PostAsJsonAsync("/api/items", new PlaceItemDto(
            "Yogurt",
            $"SKU-{Guid.NewGuid():N}",
            0.5m,
            "Cold",
            shelfId,
            5,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1))));

        place.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Expiring_items_within_days_returns_results()
    {
        var res = await _client.GetAsync("/api/items/expiring?days=600");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await res.Content.ReadFromJsonAsync<List<ItemRow>>();
        items.Should().NotBeNull();
        items!.Count.Should().BeGreaterThan(0);
    }

    private sealed record ZoneRow(Guid Id, string Name, string Type, string Temperature, decimal OccupancyRatio);
    private sealed record ShelfRow(Guid Id, Guid ZoneId, string Code, decimal Capacity, decimal CurrentLoad, decimal AvailableCapacity);
    private sealed record ItemRow(Guid Id, string Name, string Sku, decimal Weight, string RequiredTemperature, Guid ShelfId, int Quantity, DateOnly ExpiryDate);
}
