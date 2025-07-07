using AspNetDebugDashboard.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AspNetDebugDashboard.Extensions;

public static class DbContextOptionsBuilderExtensions
{
    public static DbContextOptionsBuilder AddDebugDashboard(this DbContextOptionsBuilder optionsBuilder, IServiceProvider serviceProvider)
    {
        var interceptor = serviceProvider.GetService<DebugCommandInterceptor>();
        if (interceptor != null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }
        
        return optionsBuilder;
    }
    
    public static DbContextOptionsBuilder<TContext> AddDebugDashboard<TContext>(this DbContextOptionsBuilder<TContext> optionsBuilder, IServiceProvider serviceProvider)
        where TContext : DbContext
    {
        var interceptor = serviceProvider.GetService<DebugCommandInterceptor>();
        if (interceptor != null)
        {
            optionsBuilder.AddInterceptors(interceptor);
        }
        
        return optionsBuilder;
    }
}
