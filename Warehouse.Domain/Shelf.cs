namespace Warehouse.Domain;

public class Shelf
{
    public Guid Id { get; set; }
    public Guid ZoneId { get; set; }
    public Zone Zone { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    /// <summary>Maximum total weight (kg) allowed on this shelf.</summary>
    public decimal Capacity { get; set; }
    /// <summary>Current total weight load (sum of item weight × quantity).</summary>
    public decimal CurrentLoad { get; set; }
    public ICollection<Item> Items { get; set; } = new List<Item>();
}
