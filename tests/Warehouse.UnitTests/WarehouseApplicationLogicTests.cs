using FluentAssertions;
using Warehouse.Application;
using Warehouse.Application.Parsing;
using Warehouse.Domain;
using Xunit;

namespace Warehouse.UnitTests;

public class WarehouseApplicationLogicTests
{
    [Fact]
    public void Temperature_matches_only_when_item_and_zone_share_the_same_band()
    {
        var cases = new (Temperature Item, Temperature Zone, bool Expected)[]
        {
            (Temperature.Ambient, Temperature.Ambient, true),
            (Temperature.Ambient, Temperature.Cold, false),
            (Temperature.Ambient, Temperature.Frozen, false),
            (Temperature.Cold, Temperature.Ambient, false),
            (Temperature.Cold, Temperature.Cold, true),
            (Temperature.Cold, Temperature.Frozen, false),
            (Temperature.Frozen, Temperature.Ambient, false),
            (Temperature.Frozen, Temperature.Cold, false),
            (Temperature.Frozen, Temperature.Frozen, true),
        };

        foreach (var (item, zone, expected) in cases)
        {
            WarehouseRules.TemperatureMatchesZone(item, zone).Should().Be(expected);
        }
    }

    [Fact]
    public void Shelf_capacity_uses_weight_times_quantity_against_limit()
    {
        var cases = new (decimal Current, decimal UnitWeight, int Qty, decimal Capacity, bool Fits)[]
        {
            (0m, 10m, 10, 100m, true),
            (50m, 5m, 10, 100m, true),
            (50m, 5m, 11, 100m, false),
            (99m, 1m, 1, 100m, true),
            (99m, 1m, 2, 100m, false),
            (80m, 2.5m, 8, 100m, true),
            (80m, 2.5m, 9, 100m, false),
        };

        foreach (var (current, unitWeight, qty, capacity, fits) in cases)
        {
            var additional = WarehouseRules.ComputeShelfLoad(unitWeight, qty);
            WarehouseRules.FitsCapacity(current, additional, capacity).Should().Be(fits);
        }
    }

    [Fact]
    public void Expiry_rules_align_can_include_and_is_expired_with_reference_day()
    {
        var today = new DateOnly(2026, 6, 15);
        var cases = new (DateOnly Expiry, bool CanInclude, bool IsExpired)[]
        {
            (today.AddDays(-1), false, true),
            (today, true, false),
            (today.AddDays(1), true, false),
            (today.AddDays(120), true, false),
        };

        foreach (var (expiry, canInclude, isExpired) in cases)
        {
            WarehouseRules.CanIncludeInOrder(expiry, today).Should().Be(canInclude);
            WarehouseRules.IsExpired(expiry, today).Should().Be(isExpired);
        }
    }

}
