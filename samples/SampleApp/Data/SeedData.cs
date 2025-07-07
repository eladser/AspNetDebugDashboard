using SampleApp.Models;

namespace SampleApp.Data;

public static class SeedData
{
    public static async Task SeedAsync(SampleDbContext context)
    {
        if (context.Products.Any())
            return;

        var products = new List<Product>
        {
            new Product { Name = "Laptop", Description = "High-performance laptop", Price = 999.99m, Stock = 10 },
            new Product { Name = "Mouse", Description = "Wireless mouse", Price = 29.99m, Stock = 50 },
            new Product { Name = "Keyboard", Description = "Mechanical keyboard", Price = 129.99m, Stock = 25 },
            new Product { Name = "Monitor", Description = "4K monitor", Price = 399.99m, Stock = 8 },
            new Product { Name = "Headphones", Description = "Noise-cancelling headphones", Price = 199.99m, Stock = 15 }
        };

        var customers = new List<Customer>
        {
            new Customer { Name = "John Doe", Email = "john@example.com" },
            new Customer { Name = "Jane Smith", Email = "jane@example.com" },
            new Customer { Name = "Bob Johnson", Email = "bob@example.com" }
        };

        context.Products.AddRange(products);
        context.Customers.AddRange(customers);
        await context.SaveChangesAsync();

        var orders = new List<Order>
        {
            new Order 
            { 
                CustomerId = customers[0].Id, 
                Status = "Completed",
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { ProductId = products[0].Id, Quantity = 1, UnitPrice = products[0].Price },
                    new OrderItem { ProductId = products[1].Id, Quantity = 2, UnitPrice = products[1].Price }
                }
            },
            new Order 
            { 
                CustomerId = customers[1].Id, 
                Status = "Pending",
                OrderItems = new List<OrderItem>
                {
                    new OrderItem { ProductId = products[2].Id, Quantity = 1, UnitPrice = products[2].Price }
                }
            }
        };

        foreach (var order in orders)
        {
            order.TotalAmount = order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice);
        }

        context.Orders.AddRange(orders);
        await context.SaveChangesAsync();
    }
}
