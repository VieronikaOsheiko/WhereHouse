using Microsoft.EntityFrameworkCore;
using Warehouse.Application;
using Warehouse.Application.Dtos;
using Warehouse.Infrastructure;
using Warehouse.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddWarehouseInfrastructure(
    builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Connection string 'Default' not found."));
builder.Services.AddScoped<WarehouseService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
    await db.Database.MigrateAsync();
}

var api = app.MapGroup("/api");

api.MapGet("/zones", async (WarehouseService svc, CancellationToken ct) =>
    Results.Ok(await svc.GetZonesAsync(ct)));

api.MapPost("/zones", async (CreateZoneDto dto, WarehouseService svc, CancellationToken ct) =>
{
    try
    {
        var z = await svc.CreateZoneAsync(dto, ct);
        return Results.Created($"/api/zones/{z.Id}", z);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapGet("/shelves", async (Guid? zoneId, decimal? minAvailableCapacity, WarehouseService svc, CancellationToken ct) =>
    Results.Ok(await svc.GetShelvesAsync(zoneId, minAvailableCapacity, ct)));

api.MapPost("/items", async (PlaceItemDto dto, WarehouseService svc, CancellationToken ct) =>
{
    try
    {
        var item = await svc.PlaceItemAsync(dto, ct);
        return Results.Created($"/api/items/{item.Id}", item);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

api.MapPut("/items/{id:guid}/move", async (Guid id, MoveItemDto dto, WarehouseService svc, CancellationToken ct) =>
{
    try
    {
        var item = await svc.MoveItemAsync(id, dto, ct);
        return Results.Ok(item);
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

api.MapGet("/items/expiring", async ([Microsoft.AspNetCore.Mvc.FromQuery] int days, WarehouseService svc, CancellationToken ct) =>
{
    try
    {
        return Results.Ok(await svc.GetExpiringItemsAsync(days, ct));
    }
    catch (ArgumentOutOfRangeException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

api.MapPost("/orders", async (CreateOrderDto dto, WarehouseService svc, CancellationToken ct) =>
{
    try
    {
        var order = await svc.CreateOrderAsync(dto, ct);
        return Results.Created($"/api/orders/{order.Id}", order);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

api.MapPatch("/orders/{id:guid}/status", async (Guid id, PatchOrderStatusDto dto, WarehouseService svc, CancellationToken ct) =>
{
    try
    {
        var order = await svc.PatchOrderStatusAsync(id, dto, ct);
        return Results.Ok(order);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
    catch (KeyNotFoundException ex)
    {
        return Results.NotFound(new { error = ex.Message });
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

app.Run();

public partial class Program;
