using InventoryManagementSystem.Data;
using InventoryManagementSystem.Entities;
using Microsoft.EntityFrameworkCore;

namespace InventoryManagementSystem.Services;

public class InventoryService
{
    private readonly InventoryDbContext _context;

    public InventoryService(InventoryDbContext context)
    {
        _context = context;
    }

    public async Task<int> EnsureValidCategoryAsync(string categoryName)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Name.ToLower() == categoryName.Trim().ToLower());

        if (category == null)
        {
            category = new Category { Name = categoryName.Trim() };
            _context.Categories.Add(category);
            await _context.SaveChangesAsync();
        }

        return category.Id;
    }

    public async Task TransferStockWithTransactionAsync(int wholesalerProductId, double quantity, bool simulateSystemCrash = false)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            var wholesalerProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == wholesalerProductId && p.Owner == OwnerType.Wholesaler);

            if (wholesalerProduct == null)
                throw new InvalidOperationException("Wholesaler product not found.");

            if (wholesalerProduct.StockQuantity < quantity)
                throw new InvalidOperationException($"Insufficient stock in wholesaler inventory. Available: {wholesalerProduct.StockQuantity} kg.");

            wholesalerProduct.StockQuantity -= quantity;
            await _context.SaveChangesAsync();

            if (simulateSystemCrash)
            {
                throw new Exception("Simulated hardware/network failure occurred during transfer!");
            }

            var greengrocerProduct = await _context.Products
                .FirstOrDefaultAsync(p => p.Name == wholesalerProduct.Name
                                       && p.CategoryId == wholesalerProduct.CategoryId
                                       && p.Owner == OwnerType.Greengrocer);

            if (greengrocerProduct != null)
            {
                greengrocerProduct.StockQuantity += quantity;
            }
            else
            {
                greengrocerProduct = new Product
                {
                    Name = wholesalerProduct.Name,
                    CategoryId = wholesalerProduct.CategoryId,
                    StockQuantity = quantity,
                    UnitPrice = Math.Round(wholesalerProduct.UnitPrice * 1.35m, 2),
                    Owner = OwnerType.Greengrocer
                };

                _context.Products.Add(greengrocerProduct);
            }

            await _context.SaveChangesAsync();

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<OrderItem> SellToCustomerAsync(int greengrocerProductId, double quantity)
    {
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == greengrocerProductId && p.Owner == OwnerType.Greengrocer);

        if (product == null)
            throw new InvalidOperationException("Product not found in greengrocer inventory.");

        if (product.StockQuantity < quantity)
            throw new InvalidOperationException($"Insufficient stock. Available: {product.StockQuantity} kg.");

        product.StockQuantity -= quantity;

        var orderItem = new OrderItem
        {
            ProductId = product.Id,
            Quantity = quantity,
            TotalPrice = Math.Round((decimal)quantity * product.UnitPrice, 2),
            OrderDate = DateTime.Now
        };

        _context.OrderItems.Add(orderItem);
        await _context.SaveChangesAsync();

        return orderItem;
    }

    public async Task<List<Product>> GetInventoryAsync(OwnerType owner)
    {
        return await _context.Products
            .Include(p => p.Category)
            .Where(p => p.Owner == owner)
            .ToListAsync();
    }
}