namespace Warehouse.Application.Dtos;

public record ShelfResponseDto(
    Guid Id,
    Guid ZoneId,
    string Code,
    decimal Capacity,
    decimal CurrentLoad,
    decimal AvailableCapacity);
