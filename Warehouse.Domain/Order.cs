namespace Warehouse.Domain;

public class Order
{
    public Guid Id { get; set; }
    public OrderStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ShippedAt { get; set; }
    public ICollection<OrderLine> Lines { get; set; } = new List<OrderLine>();
}
