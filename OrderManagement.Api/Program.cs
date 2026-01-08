using OrderManagement.Application.Services;
using OrderManagement.Infrastructure.Database;
using OrderManagement.Infrastructure.UnitOfWork;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// appsettings.json から接続文字列を取得
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// DB初期化
DatabaseInitializer.InitializeDatabase(connectionString);

// DI登録
builder.Services.AddControllers();
builder.Services.AddOpenApi();

// UnitOfWork は毎回新しいインスタンスを生成
// 接続文字列をクロージャでキャプチャ
builder.Services.AddScoped<Func<IUnitOfWork>>(sp =>
    () => new UnitOfWork(connectionString));

builder.Services.AddScoped<IOrderService, OrderService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();
