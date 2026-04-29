namespace Warehouse.Application.Dtos;

public record ZoneResponseDto(
    Guid Id,
    string Name,
    string Type,
    string Temperature,
    decimal OccupancyRatio);

public record CreateZoneDto(string Name, string Type, string Temperature);
