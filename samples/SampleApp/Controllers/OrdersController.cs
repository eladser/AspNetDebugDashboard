using AspNetDebugDashboard;
using Microsoft.AspNetCore.Mvc;
using SampleApp.Models;
using SampleApp.Services;

namespace SampleApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

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
