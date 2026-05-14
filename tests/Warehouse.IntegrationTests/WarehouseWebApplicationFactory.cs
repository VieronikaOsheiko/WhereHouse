using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Warehouse.Infrastructure.Persistence;
using Warehouse.Tests.Common;
using Xunit;

namespace Warehouse.IntegrationTests;

public sealed class WarehouseWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly Testcontainers.PostgreSql.PostgreSqlContainer _postgres = new Testcontainers.PostgreSql.PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    async Task IAsyncLifetime.InitializeAsync()
    {
        await _postgres.StartAsync();
        using var _ = CreateClient();
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
        await LargeDatasetSeeder.SeedAsync(db);
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        // Avoid Development-only demo seeding; integration tests use LargeDatasetSeeder (10k+).
        builder.UseSetting("ASPNETCORE_ENVIRONMENT", "Testing");
    }
}
