namespace Warehouse.Domain;

public class Item
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Sku { get; set; } = string.Empty;
    public decimal Weight { get; set; }
    public Temperature RequiredTemperature { get; set; }
    public Guid ShelfId { get; set; }
    public Shelf Shelf { get; set; } = null!;
    public int Quantity { get; set; }
    public DateOnly ExpiryDate { get; set; }
}
