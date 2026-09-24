namespace InventoryManagementSystem.Entities;
public class OrderItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public virtual Product? Product { get; set; }
    public double Quantity { get; set; }
    public decimal TotalPrice { get; set; }
    public DateTime OrderDate { get; set; }
}
