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
    public async Task Get_zones_returns_nonempty_list_with_occupancy_between_zero_and_one()
    {
        var res = await _client.GetAsync("/api/zones");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        var zones = await res.Content.ReadFromJsonAsync<List<ZoneRow>>();
        zones.Should().NotBeNull();
        zones!.Should().NotBeEmpty();
        zones.Should().OnlyContain(z => z.OccupancyRatio >= 0m && z.OccupancyRatio <= 1m);
    }

    [Fact]
    public async Task Place_item_rejects_frozen_product_on_ambient_shelf()
    {
        var zones = (await _client.GetFromJsonAsync<List<ZoneRow>>("/api/zones"))!;
        var ambient = zones.First(z => z.Temperature == "Ambient");
        var shelfId = (await _client.GetFromJsonAsync<List<ShelfRow>>($"/api/shelves?zoneId={ambient.Id}"))!.First().Id;

        var res = await _client.PostAsJsonAsync("/api/items", new PlaceItemDto(
            "Ice",
            $"SKU-{Guid.NewGuid():N}",
            1m,
            "Frozen",
            shelfId,
            1,
            DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30))));

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Post_zone_then_list_includes_that_zone()
    {
        var name = $"Zone-{Guid.NewGuid():N}";
        var create = await _client.PostAsJsonAsync("/api/zones", new CreateZoneDto(name, "Storage", "Cold"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var zones = (await _client.GetFromJsonAsync<List<ZoneRow>>("/api/zones"))!;
        zones.Should().Contain(z => z.Name == name && z.Type == "Storage" && z.Temperature == "Cold");
    }

    private sealed record ZoneRow(Guid Id, string Name, string Type, string Temperature, decimal OccupancyRatio);
    private sealed record ShelfRow(Guid Id, Guid ZoneId, string Code, decimal Capacity, decimal CurrentLoad, decimal AvailableCapacity);
}
