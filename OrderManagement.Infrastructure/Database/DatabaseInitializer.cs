using Dapper;
using Microsoft.Data.Sqlite;

namespace OrderManagement.Infrastructure.Database;

public static class DatabaseInitializer
{
    // データベース初期化
    public static void InitializeDatabase(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        // テーブル作成
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Orders (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                ProductId INTEGER NOT NULL,
                Quantity INTEGER NOT NULL,
                CreatedAt TEXT NOT NULL
            )");

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Inventory (
                ProductId INTEGER PRIMARY KEY,
                Stock INTEGER NOT NULL
            )");

        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS AuditLog (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Action TEXT NOT NULL,
                Details TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            )");

        // サンプルデータ投入
        var count = connection.ExecuteScalar<int>("SELECT COUNT(*) FROM Inventory");
        if (count == 0)
        {
            connection.Execute(@"
                INSERT INTO Inventory (ProductId, Stock) VALUES
                (1, 100),
                (2, 50),
                (3, 200)");
        }

        Console.WriteLine("Database initialized successfully.");
    }


}
