using FluentValidation;
using Microsoft.Data.Sqlite;
using OrderManagement.Api.Filters;
using OrderManagement.Api.Middleware;
using OrderManagement.Application.Services;
using OrderManagement.Application.Services.Abstractions;
using OrderManagement.Infrastructure.Database;
using OrderManagement.Infrastructure.UnitOfWork.ActionScope;
using Scalar.AspNetCore;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// appsettings.json から接続文字列を取得
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// ===== データベース初期化 =====
DatabaseInitializer.Initialize(connectionString);

// DI登録
// Controller + 自前の ValidationFilter
builder.Services.AddControllers(options =>
{
    // グローバルに ValidationFilter を適用
    options.Filters.Add<ValidationFilter>();
});

builder.Services.AddOpenApi();

// FluentValidation（Validator のみ登録）
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// Database
builder.Services.AddScoped<IDbConnection>(sp =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not found.");

    var conn = new SqliteConnection(connectionString);
    conn.Open();
    return conn;
});
// unit of work
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Services
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// ミドルウェア（例外ハンドリング用）
app.UseMiddleware<ProblemDetailsMiddleware>();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
