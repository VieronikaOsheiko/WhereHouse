using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Application.Persistence;
using Warehouse.Infrastructure.Persistence;

namespace Warehouse.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWarehouseInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        var migrationsAssembly = typeof(WarehouseDbContext).Assembly.GetName().Name;
        services.AddDbContext<WarehouseDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(migrationsAssembly)));

        services.AddScoped<IWarehouseDbContext>(sp => sp.GetRequiredService<WarehouseDbContext>());
        return services;
    }
}
