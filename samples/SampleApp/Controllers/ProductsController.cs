using AspNetDebugDashboard;
using Microsoft.AspNetCore.Mvc;
using SampleApp.Models;
using SampleApp.Services;

namespace SampleApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAll()
    {
        try
        {
            var products = await _productService.GetAllAsync();
            return Ok(products);
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"Error fetching products: {ex.Message}", "ProductsController");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetById(int id)
    {
        try
        {
            var product = await _productService.GetByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }
            return Ok(product);
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"Error fetching product {id}: {ex.Message}", "ProductsController");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost]
    public async Task<ActionResult<Product>> Create(Product product)
    {
        try
        {
            var createdProduct = await _productService.CreateAsync(product);
            return CreatedAtAction(nameof(GetById), new { id = createdProduct.Id }, createdProduct);
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"Error creating product: {ex.Message}", "ProductsController");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Product>> Update(int id, Product product)
    {
        try
        {
            var updatedProduct = await _productService.UpdateAsync(id, product);
            if (updatedProduct == null)
            {
                return NotFound();
            }
            return Ok(updatedProduct);
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"Error updating product {id}: {ex.Message}", "ProductsController");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var result = await _productService.DeleteAsync(id);
            if (!result)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"Error deleting product {id}: {ex.Message}", "ProductsController");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("search")]
    public async Task<ActionResult<List<Product>>> Search(string q)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return BadRequest("Search query cannot be empty");
            }

            var products = await _productService.SearchAsync(q);
            return Ok(products);
        }
        catch (Exception ex)
        {
            await DebugLogger.ErrorAsync($"Error searching products: {ex.Message}", "ProductsController");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpGet("test-error")]
    public async Task<ActionResult> TestError()
    {
        await DebugLogger.InfoAsync("Testing error handling", "ProductsController");
        
        // This will cause an intentional error for testing
        throw new InvalidOperationException("This is a test exception to demonstrate error logging");
    }

    [HttpGet("slow-operation")]
    public async Task<ActionResult> SlowOperation()
    {
        await DebugLogger.InfoAsync("Starting slow operation", "ProductsController");
        
        // Simulate a slow operation
        await Task.Delay(2000);
        
        await DebugLogger.InfoAsync("Slow operation completed", "ProductsController");
        
        return Ok(new { message = "Slow operation completed" });
    }
}
