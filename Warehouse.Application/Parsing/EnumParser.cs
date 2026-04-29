using Warehouse.Domain;

namespace Warehouse.Application.Parsing;

public static class EnumParser
{
    public static bool TryParseZoneType(string value, out ZoneType type)
    {
        type = default;
        return Enum.TryParse(value, ignoreCase: true, out type);
    }

    public static bool TryParseTemperature(string value, out Temperature temperature)
    {
        temperature = default;
        return Enum.TryParse(value, ignoreCase: true, out temperature);
    }

    public static bool TryParseOrderStatus(string value, out OrderStatus status)
    {
        status = default;
        return Enum.TryParse(value, ignoreCase: true, out status);
    }
}
