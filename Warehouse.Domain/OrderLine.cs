namespace Warehouse.Domain;

public class OrderLine
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;
    /// <summary>Set when the line is created; cleared if inventory row is removed after shipping.</summary>
    public Guid? ItemId { get; set; }
    public Item? Item { get; set; }
    public int Quantity { get; set; }
}
