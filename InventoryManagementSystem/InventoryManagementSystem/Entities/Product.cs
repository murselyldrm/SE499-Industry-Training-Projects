namespace InventoryManagementSystem.Entities;
public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public virtual Category? Category { get; set; }
    public double StockQuantity { get; set; }
    public decimal UnitPrice { get; set; }
    public OwnerType Owner { get; set; }
    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}