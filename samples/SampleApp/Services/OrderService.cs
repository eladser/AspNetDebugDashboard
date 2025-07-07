using AspNetDebugDashboard;
using Microsoft.EntityFrameworkCore;
using SampleApp.Data;
using SampleApp.Models;

namespace SampleApp.Services;

public interface IOrderService
{
    Task<List<Order>> GetAllAsync();
    Task<Order?> GetByIdAsync(int id);
    Task<Order> CreateAsync(Order order);
    Task<Order?> UpdateStatusAsync(int id, string status);
    Task<List<Order>> GetByCustomerIdAsync(int customerId);
}

public class OrderService : IOrderService
{
    private readonly SampleDbContext _context;

    public OrderService(SampleDbContext context)
    {
        _context = context;
    }

    public async Task<List<Order>> GetAllAsync()
    {
        await DebugLogger.InfoAsync("Fetching all orders", "OrderService");
        
        var orders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
            
        await DebugLogger.InfoAsync($"Found {orders.Count} orders", "OrderService", 
            new Dictionary<string, object> { { "Count", orders.Count } });
            
        return orders;
    }

    public async Task<Order?> GetByIdAsync(int id)
    {
        await DebugLogger.InfoAsync($"Fetching order with ID: {id}", "OrderService");
        
        var order = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .FirstOrDefaultAsync(o => o.Id == id);
            
        if (order == null)
        {
            await DebugLogger.WarningAsync($"Order with ID {id} not found", "OrderService");
        }
        
        return order;
    }

    public async Task<Order> CreateAsync(Order order)
    {
        await DebugLogger.InfoAsync($"Creating new order for customer: {order.CustomerId}", "OrderService");
        
        // Calculate total amount
        order.TotalAmount = order.OrderItems.Sum(oi => oi.Quantity * oi.UnitPrice);
        
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        
        await DebugLogger.SuccessAsync($"Order created with ID: {order.Id}", "OrderService", 
            new Dictionary<string, object> 
            { 
                { "OrderId", order.Id }, 
                { "CustomerId", order.CustomerId },
                { "TotalAmount", order.TotalAmount },
                { "ItemCount", order.OrderItems.Count }
            });
        
        return order;
    }

    public async Task<Order?> UpdateStatusAsync(int id, string status)
    {
        await DebugLogger.InfoAsync($"Updating order {id} status to: {status}", "OrderService");
        
        var order = await _context.Orders.FindAsync(id);
        if (order == null)
        {
            await DebugLogger.WarningAsync($"Order with ID {id} not found for status update", "OrderService");
            return null;
        }

        var oldStatus = order.Status;
        order.Status = status;
        await _context.SaveChangesAsync();
        
        await DebugLogger.SuccessAsync($"Order {id} status updated from {oldStatus} to {status}", "OrderService");
        
        return order;
    }

    public async Task<List<Order>> GetByCustomerIdAsync(int customerId)
    {
        await DebugLogger.InfoAsync($"Fetching orders for customer: {customerId}", "OrderService");
        
        var orders = await _context.Orders
            .Include(o => o.Customer)
            .Include(o => o.OrderItems)
            .ThenInclude(oi => oi.Product)
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.OrderDate)
            .ToListAsync();
            
        await DebugLogger.InfoAsync($"Found {orders.Count} orders for customer {customerId}", "OrderService", 
            new Dictionary<string, object> { { "CustomerId", customerId }, { "Count", orders.Count } });
            
        return orders;
    }
}
