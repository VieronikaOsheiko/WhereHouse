namespace Warehouse.Domain;

public class Zone
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ZoneType Type { get; set; }
    public Temperature Temperature { get; set; }
    public ICollection<Shelf> Shelves { get; set; } = new List<Shelf>();
}
