using Dapper;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Moq;
using OrderManagement.Domain.Entities;
using OrderManagement.Infrastructure.Database;
using OrderManagement.Infrastructure.UnitOfWork.Basic;
using System.Data;
using Xunit;

namespace OrderManagement.Tests.IntegrationTests;

/// <summary>
/// UnitOfWork の統合テスト
/// 実際のSQLiteを使用してトランザクション管理を検証
/// </summary>
public class UnitOfWorkIntegrationTests : IDisposable
{
    private readonly string _connectionString;
    private readonly IDbConnection _testConnection;

    public UnitOfWorkIntegrationTests()
    {
        _connectionString = "Data Source=:memory:";
        _testConnection = new SqliteConnection(_connectionString);
        _testConnection.Open();
        DatabaseInitializer.Initialize(_testConnection);
    }


    //private void InitializeTestDatabase()
    //{
    //    _testConnection.Execute(@"
    //            CREATE TABLE Orders (
    //                Id INTEGER PRIMARY KEY AUTOINCREMENT,
    //                ProductId INTEGER NOT NULL,
    //                Quantity INTEGER NOT NULL,
    //                CreatedAt TEXT NOT NULL
    //            )");

    //    _testConnection.Execute(@"
    //            CREATE TABLE Inventory (
    //                ProductId INTEGER PRIMARY KEY,
    //                Stock INTEGER NOT NULL
    //            )");

    //    _testConnection.Execute(@"
    //            CREATE TABLE AuditLog (
    //                Id INTEGER PRIMARY KEY AUTOINCREMENT,
    //                Action TEXT NOT NULL,
    //                Details TEXT NOT NULL,
    //                CreatedAt TEXT NOT NULL
    //            )");

    //    _testConnection.Execute(@"
    //            INSERT INTO Inventory (ProductId, Stock) VALUES
    //            (1, 100), (2, 50), (3, 200)");
    //}


    [Fact]
    public async Task CommitAsync_正常系_データが永続化される()
    {
        // Arrange
        using var uow = new UnitOfWork(_testConnection);
        uow.BeginTransaction();

        var order = new Order
        {
            Id = 1,
            CustomerId = 1,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var orderId = await uow.Orders.CreateAsync(order);
        uow.Commit();

        // Assert
        var savedOrder = _testConnection.QueryFirstOrDefault<Order>(
            "SELECT * FROM Orders WHERE Id = @Id", new { Id = orderId });

        savedOrder.Should().NotBeNull();
        savedOrder!.Id.Should().Be(1);
        savedOrder.CustomerId.Should().Be(1);
    }

    [Fact]
    public async Task RollbackAsync_異常系_データがロールバックされる()
    {
        // Arrange
        using var uow = new UnitOfWork(_testConnection);
        uow.BeginTransaction();

        var order = new Order
        {
            Id = 1,
            CustomerId = 10,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await uow.Orders.CreateAsync(order);
        uow.Rollback();

        // Assert
        var count = _testConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM Orders");
        count.Should().Be(0);
    }

    [Fact]
    public async Task 複数Repository_同一トランザクションで実行される()
    {
        // Arrange
        using var uow = new UnitOfWork(_testConnection);
        uow.BeginTransaction();

        // Act
        var inventory = await uow.Inventory.GetByProductIdAsync(1);
        await uow.Inventory.UpdateStockAsync(1, inventory!.Stock - 10);

        var order = new Order
        {
            Id = 1,
            CustomerId = 10,
            CreatedAt = DateTime.UtcNow
        };
        await uow.Orders.CreateAsync(order);

        var auditLog = new AuditLog
        {
            Action = "TEST",
            Details = "Integration test",
            CreatedAt = DateTime.UtcNow
        };
        await uow.AuditLogs.CreateAsync(auditLog);

        uow.Commit();

        // Assert
        var updatedStock = _testConnection.ExecuteScalar<int>(
            "SELECT Stock FROM Inventory WHERE ProductId = 1");
        updatedStock.Should().Be(90);

        var orderCount = _testConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM Orders");
        orderCount.Should().Be(1);

        var logCount = _testConnection.ExecuteScalar<int>("SELECT COUNT(*) FROM AuditLog");
        logCount.Should().Be(1);
    }

    [Fact]
    public void BeginTransaction_二重開始_例外をスローする()
    {
        // Arrange
        using var uow = new UnitOfWork(_testConnection);
        uow.BeginTransaction();

        // Act & Assert
        var act = () => uow.BeginTransaction();
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("Transaction is already started.");
    }

    [Fact]
    public async Task CommitAsync_トランザクション未開始_例外をスローする()
    {
        // Arrange
        using var uow = new UnitOfWork(_testConnection);

        // Act & Assert
        var act = async () => uow.Commit();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Transaction is not started.");
    }

    [Fact]
    public async Task RollbackAsync_トランザクション未開始_例外をスローする()
    {
        // Arrange
        using var uow = new UnitOfWork(_testConnection);

        // Act & Assert
        var act = async () => uow.Commit();
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Transaction is not started.");
    }


    public void Dispose()
    {
        _testConnection?.Dispose();
        GC.SuppressFinalize(this);
    }
}