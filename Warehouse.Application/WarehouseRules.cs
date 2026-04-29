using Warehouse.Domain;

namespace Warehouse.Application;

public static class WarehouseRules
{
    public static bool TemperatureMatchesZone(Temperature itemRequired, Temperature zoneTemperature) =>
        itemRequired == zoneTemperature;

    public static decimal ComputeShelfLoad(decimal itemWeight, int quantity) => itemWeight * quantity;

    public static decimal ComputeTotalShelfLoad(IEnumerable<(decimal Weight, int Quantity)> lines) =>
        lines.Sum(l => ComputeShelfLoad(l.Weight, l.Quantity));

    public static bool FitsCapacity(decimal currentLoad, decimal additionalLoad, decimal capacity) =>
        currentLoad + additionalLoad <= capacity;

    public static bool IsExpired(DateOnly expiryDate, DateOnly today) => expiryDate < today;

    public static bool CanIncludeInOrder(DateOnly expiryDate, DateOnly today) => !IsExpired(expiryDate, today);
}
