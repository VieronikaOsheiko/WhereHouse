using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Warehouse.Infrastructure.Persistence;
using Xunit;

namespace Warehouse.DatabaseTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => _postgres.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var db = new WarehouseDbContext(options);
        await db.Database.MigrateAsync();
    }

    public async Task ResetDatabaseAsync()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var db = new WarehouseDbContext(options);
        await db.Database.ExecuteSqlRawAsync(
            """
            TRUNCATE TABLE "OrderLines", "Orders", "Items", "Shelves", "Zones" RESTART IDENTITY CASCADE;
            """);
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();
}
