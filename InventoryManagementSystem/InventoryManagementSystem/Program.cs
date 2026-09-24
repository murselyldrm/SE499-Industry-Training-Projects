using InventoryManagementSystem.Data;
using InventoryManagementSystem.Entities;
using InventoryManagementSystem.Services;
using Microsoft.EntityFrameworkCore;

using var context = new InventoryDbContext();
await context.Database.EnsureCreatedAsync();
var service = new InventoryService(context);

if (!await context.Products.AnyAsync(p => p.Owner == OwnerType.Wholesaler))
{
    var fruitCatId = await service.EnsureValidCategoryAsync("Fruit");
    var vegCatId = await service.EnsureValidCategoryAsync("Vegetable");

    context.Products.AddRange(
        new Product { Name = "Gala Apples", CategoryId = fruitCatId, StockQuantity = 100.0, UnitPrice = 2.00m, Owner = OwnerType.Wholesaler },
        new Product { Name = "Organic Bananas", CategoryId = fruitCatId, StockQuantity = 150.0, UnitPrice = 1.50m, Owner = OwnerType.Wholesaler },
        new Product { Name = "Vine Tomatoes", CategoryId = vegCatId, StockQuantity = 80.0, UnitPrice = 2.40m, Owner = OwnerType.Wholesaler }
    );
    await context.SaveChangesAsync();
}

while (true)
{
    Console.WriteLine("\n=======================================================");
    Console.WriteLine("    SUPPLY CHAIN & INVENTORY MANAGEMENT SYSTEM         ");
    Console.WriteLine("=======================================================");
    Console.WriteLine("[1] View Wholesaler Inventory");
    Console.WriteLine("[2] View Greengrocer Inventory");
    Console.WriteLine("[3] Transfer Stock (Wholesaler -> Greengrocer)");
    Console.WriteLine("[4] Customer Retail Sale");
    Console.WriteLine("[5] Simulate Crash & Database Rollback (ACID Test)");
    Console.WriteLine("[0] Exit");
    Console.Write("\nSelect an option: ");

    var choice = Console.ReadLine()?.Trim();

    switch (choice)
    {
        case "1":
            await PrintInventoryAsync(OwnerType.Wholesaler, "Wholesaler Warehouse");
            break;

        case "2":
            await PrintInventoryAsync(OwnerType.Greengrocer, "Greengrocer Retail Store");
            break;

        case "3":
            await HandleStockTransferAsync();
            break;

        case "4":
            await HandleCustomerSaleAsync();
            break;

        case "5":
            await HandleCrashSimulationAsync();
            break;

        case "0":
            Console.WriteLine("Exiting application. Goodbye!");
            return;

        default:
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("Invalid selection. Please choose an option from the menu.");
            Console.ResetColor();
            break;
    }
}

async Task PrintInventoryAsync(OwnerType owner, string title)
{
    Console.WriteLine($"\n--- {title} ---");
    var items = await service.GetInventoryAsync(owner);

    if (items.Count == 0)
    {
        Console.WriteLine("Inventory is currently empty.");
        return;
    }

    Console.WriteLine($"{"ID",-5} {"Product Name",-20} {"Category",-12} {"Stock (kg)",-12} {"Unit Price",-10}");
    Console.WriteLine(new string('-', 62));
    foreach (var p in items)
    {
        Console.WriteLine($"{p.Id,-5} {p.Name,-20} {p.Category?.Name,-12} {p.StockQuantity,-12:F1} ${p.UnitPrice,-10:F2}");
    }
}

async Task HandleStockTransferAsync()
{
    await PrintInventoryAsync(OwnerType.Wholesaler, "Wholesaler Warehouse (Available for Transfer)");

    Console.Write("\nEnter Wholesaler Product ID: ");
    if (!int.TryParse(Console.ReadLine(), out int productId))
    {
        ShowError("Invalid Product ID.");
        return;
    }

    Console.Write("Enter quantity to transfer (kg): ");
    if (!double.TryParse(Console.ReadLine(), out double quantity) || quantity <= 0)
    {
        ShowError("Invalid quantity.");
        return;
    }

    try
    {
        await service.TransferStockWithTransactionAsync(productId, quantity);
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"[SUCCESS] Successfully transferred {quantity:F1} kg to Greengrocer with 35% margin applied.");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        ShowError(ex.Message);
    }
}

async Task HandleCustomerSaleAsync()
{
    await PrintInventoryAsync(OwnerType.Greengrocer, "Greengrocer Retail Store (Available for Purchase)");

    Console.Write("\nEnter Greengrocer Product ID: ");
    if (!int.TryParse(Console.ReadLine(), out int productId))
    {
        ShowError("Invalid Product ID.");
        return;
    }

    Console.Write("Enter purchase quantity (kg): ");
    if (!double.TryParse(Console.ReadLine(), out double quantity) || quantity <= 0)
    {
        ShowError("Invalid quantity.");
        return;
    }

    try
    {
        var order = await service.SellToCustomerAsync(productId, quantity);
        var product = (await service.GetInventoryAsync(OwnerType.Greengrocer)).First(p => p.Id == productId);

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("\n================ SALE RECEIPT ================");
        Console.WriteLine($"Receipt ID   : #{order.Id}");
        Console.WriteLine($"Product      : {product.Name}");
        Console.WriteLine($"Quantity     : {order.Quantity:F1} kg");
        Console.WriteLine($"Unit Price   : ${product.UnitPrice:F2}/kg");
        Console.WriteLine($"Total Amount : ${order.TotalPrice:F2}");
        Console.WriteLine("==============================================");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        ShowError(ex.Message);
    }
}

async Task HandleCrashSimulationAsync()
{
    var wholesalerStock = await service.GetInventoryAsync(OwnerType.Wholesaler);
    if (wholesalerStock.Count == 0)
    {
        ShowError("No wholesaler stock available to test.");
        return;
    }

    var targetProduct = wholesalerStock.First();
    double testQty = 15.0;
    double initialStock = targetProduct.StockQuantity;

    Console.WriteLine($"\n[TRANSACTION TEST] Target Product: {targetProduct.Name}");
    Console.WriteLine($"[TRANSACTION TEST] Current Wholesaler Stock: {initialStock:F1} kg");
    Console.WriteLine($"[TRANSACTION TEST] Attempting transfer of {testQty:F1} kg with SIMULATED CRASH enabled...\n");

    try
    {
        await service.TransferStockWithTransactionAsync(targetProduct.Id, testQty, simulateSystemCrash: true);
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[EXCEPTION TRIGGERED] {ex.Message}");
        Console.ResetColor();
    }

    context.ChangeTracker.Clear();
    var verifyProduct = (await service.GetInventoryAsync(OwnerType.Wholesaler)).First(p => p.Id == targetProduct.Id);

    Console.WriteLine("\n--- VERIFYING TRANSACTION ROLLBACK ---");
    Console.WriteLine($"Expected Stock after rollback : {initialStock:F1} kg");
    Console.WriteLine($"Actual Stock in SQL Server    : {verifyProduct.StockQuantity:F1} kg");

    if (Math.Abs(verifyProduct.StockQuantity - initialStock) < 0.001)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("[ACID PASS] Database transaction rolled back successfully. No data corruption or phantom deduction occurred!");
        Console.ResetColor();
    }
    else
    {
        ShowError("[FAIL] Stock mismatch detected.");
    }
}

void ShowError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[ERROR] {message}");
    Console.ResetColor();
}