using FluentValidation;
using Microsoft.Data.Sqlite;
using OrderManagement.Api.Filters;
using OrderManagement.Api.Middleware;
using OrderManagement.Application.Common;
using OrderManagement.Application.Repositories;
using OrderManagement.Application.Services;
using OrderManagement.Application.Services.Abstractions;
using OrderManagement.Infrastructure.Persistence;
using OrderManagement.Infrastructure.Persistence.Database;
using OrderManagement.Infrastructure.Persistence.Repositories;
using Scalar.AspNetCore;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// 1. 設定の取得
// appsettings.json から接続文字列を取得
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// 2. DI 登録 (Infrastructure / Database)
// 先に IDbConnection を登録することで、後続の DbSession で再利用可能にする
builder.Services.AddScoped<IDbConnection>(sp => new SqliteConnection(connectionString));
// DbSession
builder.Services.AddScoped<DbSession>();
// IDbSessionManager (UnitOfWork用)
builder.Services.AddScoped<IDbSessionManager>(sp => sp.GetRequiredService<DbSession>());
// IDbSession (リポジトリ用)
builder.Services.AddScoped<IDbSession>(sp => sp.GetRequiredService<DbSession>());
// UnitOfWork
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

// Repositories
builder.Services.AddScoped<IAuditLogRepository, AuditLogRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Services
builder.Services.AddScoped<IAuditLogService, AuditLogService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IOrderService, OrderService>();

// 4. DI 登録 (Framework / Web)
builder.Services.AddControllers(options =>
{
    // グローバルに ValidationFilter を適用
    options.Filters.Add<ValidationFilter>();
});
builder.Services.AddOpenApi();
// FluentValidation（Validator のみ登録）
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// アプリケーションのビルド
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// 5. データベースの初期化 (app.Build() の後で実行)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // 登録済みの IDbConnection を使って初期化
        var connection = services.GetRequiredService<IDbConnection>();
        DatabaseInitializer.Initialize(connection);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "データベース初期化中にエラーが発生しました。");
        throw; // 致命的なエラーとして停止させる
    }
}

// ミドルウェア（例外ハンドリング用）
app.UseMiddleware<ProblemDetailsMiddleware>();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
