using AspNetDebugDashboard;
using Microsoft.EntityFrameworkCore;
using SampleApp.Data;
using SampleApp.Models;

namespace SampleApp.Services;

public interface IProductService
{
    Task<List<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(int id);
    Task<Product> CreateAsync(Product product);
    Task<Product?> UpdateAsync(int id, Product product);
    Task<bool> DeleteAsync(int id);
    Task<List<Product>> SearchAsync(string query);
}

public class ProductService : IProductService
{
    private readonly SampleDbContext _context;

    public ProductService(SampleDbContext context)
    {
        _context = context;
    }

    public async Task<List<Product>> GetAllAsync()
    {
        await DebugLogger.InfoAsync("Fetching all products", "ProductService");
        
        var products = await _context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .ToListAsync();
            
        await DebugLogger.InfoAsync($"Found {products.Count} products", "ProductService", 
            new Dictionary<string, object> { { "Count", products.Count } });
            
        return products;
    }

    public async Task<Product?> GetByIdAsync(int id)
    {
        await DebugLogger.InfoAsync($"Fetching product with ID: {id}", "ProductService");
        
        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
            
        if (product == null)
        {
            await DebugLogger.WarningAsync($"Product with ID {id} not found", "ProductService");
        }
        
        return product;
    }

    public async Task<Product> CreateAsync(Product product)
    {
        await DebugLogger.InfoAsync($"Creating new product: {product.Name}", "ProductService");
        
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        
        await DebugLogger.SuccessAsync($"Product created with ID: {product.Id}", "ProductService");
        
        return product;
    }

    public async Task<Product?> UpdateAsync(int id, Product product)
    {
        await DebugLogger.InfoAsync($"Updating product with ID: {id}", "ProductService");
        
        var existingProduct = await _context.Products.FindAsync(id);
        if (existingProduct == null)
        {
            await DebugLogger.WarningAsync($"Product with ID {id} not found for update", "ProductService");
            return null;
        }

        existingProduct.Name = product.Name;
        existingProduct.Description = product.Description;
        existingProduct.Price = product.Price;
        existingProduct.Stock = product.Stock;

        await _context.SaveChangesAsync();
        
        await DebugLogger.SuccessAsync($"Product {id} updated successfully", "ProductService");
        
        return existingProduct;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        await DebugLogger.InfoAsync($"Deleting product with ID: {id}", "ProductService");
        
        var product = await _context.Products.FindAsync(id);
        if (product == null)
        {
            await DebugLogger.WarningAsync($"Product with ID {id} not found for deletion", "ProductService");
            return false;
        }

        product.IsActive = false; // Soft delete
        await _context.SaveChangesAsync();
        
        await DebugLogger.SuccessAsync($"Product {id} deleted successfully", "ProductService");
        
        return true;
    }

    public async Task<List<Product>> SearchAsync(string query)
    {
        await DebugLogger.InfoAsync($"Searching products with query: {query}", "ProductService");
        
        var products = await _context.Products
            .Where(p => p.IsActive && (p.Name.Contains(query) || p.Description!.Contains(query)))
            .OrderBy(p => p.Name)
            .ToListAsync();
            
        await DebugLogger.InfoAsync($"Search returned {products.Count} products", "ProductService", 
            new Dictionary<string, object> { { "Query", query }, { "Count", products.Count } });
            
        return products;
    }
}
