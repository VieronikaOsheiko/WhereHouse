namespace Warehouse.Application.Dtos;

public record PlaceItemDto(
    string Name,
    string Sku,
    decimal Weight,
    string RequiredTemperature,
    Guid ShelfId,
    int Quantity,
    DateOnly ExpiryDate);

public record MoveItemDto(Guid TargetShelfId);

public record ItemResponseDto(
    Guid Id,
    string Name,
    string Sku,
    decimal Weight,
    string RequiredTemperature,
    Guid ShelfId,
    int Quantity,
    DateOnly ExpiryDate);
