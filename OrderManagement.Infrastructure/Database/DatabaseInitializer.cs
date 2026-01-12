using Dapper;
using Microsoft.Data.Sqlite;
using System.Data;

namespace OrderManagement.Infrastructure.Database;

/// <summary>
/// データベース初期化クラス
/// </summary>
public static class DatabaseInitializer
{
    /// <summary>
    /// データベースを初期化します（テーブル作成 + サンプルデータ投入）
    /// </summary>
    /// <param name="connectionString">接続文字列</param>
    public static void Initialize(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        CreateTables(connection);
        SeedData(connection);

        Console.WriteLine("Database initialized successfully.");
    }

    public static void Initialize(IDbConnection connection)
    {
        if (connection.State != ConnectionState.Open)
            connection.Open();

        CreateTables(connection);
        SeedData(connection);

        Console.WriteLine("Database initialized successfully.");
    }


    /// <summary>
    /// テーブルを作成します
    /// </summary>
    private static void CreateTables(IDbConnection connection)
    {
        // Orders テーブル
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                CustomerId INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL
            )
            """);

        // OrderDetails テーブル（新規追加）
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS OrderDetails (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                OrderId INTEGER NOT NULL,
                ProductId INTEGER NOT NULL,
                Quantity INTEGER NOT NULL,
                UnitPrice REAL NOT NULL,
                FOREIGN KEY (OrderId) REFERENCES Orders(Id)
            )
            """);

        // Inventory テーブル
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS Inventory (
                ProductId INTEGER PRIMARY KEY,
                ProductName TEXT NOT NULL,
                Stock INTEGER NOT NULL,
                UnitPrice REAL NOT NULL
            )
            """);

        // AuditLog テーブル
        connection.Execute("""
            CREATE TABLE IF NOT EXISTS AuditLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Action TEXT NOT NULL,
                Details TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            )
            """);
    }

    /// <summary>
    /// サンプルデータを投入します
    /// </summary>
    private static void SeedData(IDbConnection connection)
    {
        var count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Inventory");
        if (count == 0)
        {
            connection.Execute("""
                INSERT INTO Inventory (ProductId, ProductName, Stock, UnitPrice) VALUES
                (1, 'ノートPC', 100, 120000.00),
                (2, 'マウス', 50, 2500.00),
                (3, 'キーボード', 200, 8000.00)
                """);
        }
    }
}
