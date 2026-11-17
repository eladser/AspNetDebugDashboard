using AspNetDebugDashboard;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SampleApp.Data;
using SampleApp.Models;
using SampleApp.Services;

namespace SampleApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;
    private static ShippingRates? _rates;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Order>>> GetAll()
    {
        try
        {
            var orders = await _orderService.GetAllAsync();
            return Ok(orders);
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"Error fetching orders: {ex.Message}", "OrdersController");
            return StatusCode(500, "Internal server error");
        }
    }

    // Slow on purpose, for trying the Timeline and Replay. It loads the items with one query
    // per order (N+1), then waits on a 300 ms shipping-rates call. Replay it with
    // ?batched=true for the fixed version: one query for all the items, and the rates
    // come from the previous call instead of a new one.
    [HttpGet("report")]
    public async Task<IActionResult> Report(
        [FromServices] SampleDbContext db, [FromServices] IHttpClientFactory http, bool batched = false)
    {
        var recent = db.Orders.OrderByDescending(o => o.Id).Take(20);
        List<Order> orders;
        if (batched)
        {
            orders = await recent.Include(o => o.OrderItems).ToListAsync();
        }
        else
        {
            orders = await recent.ToListAsync();
            foreach (var o in orders)
                o.OrderItems = await db.OrderItems.Where(i => i.OrderId == o.Id).ToListAsync();
        }

        if (!batched || _rates == null)
            _rates = await http.CreateClient().GetFromJsonAsync<ShippingRates>($"{Request.Scheme}://{Request.Host}/internal/shipping-rates");

        return Ok(new
        {
            shipping = _rates,
            orders = orders.Select(o => new { o.Id, o.Status, items = o.OrderItems.Count, total = o.OrderItems.Sum(i => i.Quantity * i.UnitPrice) })
        });
    }

    // Stands in for a slow upstream service. /internal/ is in ExcludedPaths (Program.cs),
    // so this shows up as the report's outgoing call, not as a request of its own.
    [HttpGet("/internal/shipping-rates")]
    public async Task<ShippingRates> Rates()
    {
        await Task.Delay(300);
        return new ShippingRates(4.99m, 14.99m);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Order>> GetById(int id)
    {
        try
        {
            var order = await _orderService.GetByIdAsync(id);
            if (order == null)
            {
                return NotFound();
            }
            return Ok(order);
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"Error fetching order {id}: {ex.Message}", "OrdersController");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Order>> Create(Order order)
    {
        try
        {
            var createdOrder = await _orderService.CreateAsync(order);
            DebugDump.Dump(createdOrder, "created order");
            return CreatedAtAction(nameof(GetById), new { id = createdOrder.Id }, createdOrder);
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"Error creating order: {ex.Message}", "OrdersController");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}/status")]
    public async Task<ActionResult<Order>> UpdateStatus(int id, [FromBody] string status)
    {
        try
        {
            var updatedOrder = await _orderService.UpdateStatusAsync(id, status);
            if (updatedOrder == null)
            {
                return NotFound();
            }
            return Ok(updatedOrder);
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"Error updating order {id} status: {ex.Message}", "OrdersController");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("customer/{customerId}")]
    public async Task<ActionResult<List<Order>>> GetByCustomerId(int customerId)
    {
        try
        {
            var orders = await _orderService.GetByCustomerIdAsync(customerId);
            return Ok(orders);
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"Error fetching orders for customer {customerId}: {ex.Message}", "OrdersController");
            return StatusCode(500, "Internal server error");
        }
    }
}
