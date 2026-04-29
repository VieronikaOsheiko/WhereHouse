using FluentAssertions;
using Warehouse.Application;
using Warehouse.Domain;
using Xunit;

namespace Warehouse.UnitTests;

public class WarehouseRulesTests
{
    [Theory]
    [InlineData(Temperature.Ambient, Temperature.Ambient, true)]
    [InlineData(Temperature.Cold, Temperature.Cold, true)]
    [InlineData(Temperature.Frozen, Temperature.Frozen, true)]
    [InlineData(Temperature.Frozen, Temperature.Ambient, false)]
    [InlineData(Temperature.Cold, Temperature.Frozen, false)]
    public void Temperature_matches_zone_only_when_equal(Temperature item, Temperature zone, bool expected)
    {
        WarehouseRules.TemperatureMatchesZone(item, zone).Should().Be(expected);
    }

    [Fact]
    public void Shelf_load_is_weight_times_quantity()
    {
        WarehouseRules.ComputeShelfLoad(2.5m, 4).Should().Be(10m);
        WarehouseRules.ComputeTotalShelfLoad([(3m, 2), (1m, 5)]).Should().Be(11m);
    }

    [Fact]
    public void Capacity_fit_checks_additional_load()
    {
        WarehouseRules.FitsCapacity(100m, 50m, 200m).Should().BeTrue();
        WarehouseRules.FitsCapacity(180m, 50m, 200m).Should().BeFalse();
    }

    [Fact]
    public void Expiry_validation_uses_calendar_dates()
    {
        var today = new DateOnly(2026, 4, 30);
        WarehouseRules.IsExpired(today.AddDays(-1), today).Should().BeTrue();
        WarehouseRules.CanIncludeInOrder(today, today).Should().BeTrue();
        WarehouseRules.CanIncludeInOrder(today.AddDays(1), today).Should().BeTrue();
    }
}
