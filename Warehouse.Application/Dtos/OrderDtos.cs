namespace Warehouse.Application.Dtos;

public record OrderLineRequestDto(Guid ItemId, int Quantity);

public record CreateOrderDto(IReadOnlyList<OrderLineRequestDto> Lines);

public record PatchOrderStatusDto(string Status);

public record OrderResponseDto(
    Guid Id,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ShippedAt,
    IReadOnlyList<OrderLineResponseDto> Lines);

public record OrderLineResponseDto(Guid? ItemId, int Quantity);
