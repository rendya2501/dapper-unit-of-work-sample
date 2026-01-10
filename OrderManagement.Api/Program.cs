using FluentValidation;
using OrderManagement.Api.Filters;
using OrderManagement.Api.Middleware;
using OrderManagement.Application.Services;
using OrderManagement.Application.Services.Abstractions;
using OrderManagement.Infrastructure.Database;
using OrderManagement.Infrastructure.UnitOfWork;
using Scalar.AspNetCore;

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

// UnitOfWork Factory
builder.Services.AddScoped<Func<IUnitOfWork>>(sp =>
    () => new UnitOfWork(connectionString));

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
app.UseMiddleware<ValidationExceptionMiddleware>();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
