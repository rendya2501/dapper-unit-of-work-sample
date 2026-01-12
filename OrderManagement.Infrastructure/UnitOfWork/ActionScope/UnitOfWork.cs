using Microsoft.Extensions.Logging;
using System.Data;

namespace OrderManagement.Infrastructure.UnitOfWork.ActionScope;

/// <summary>
/// Unit of Work の実装クラス（Action Scope パターン）
/// </summary>
/// <remarks>
/// コンストラクタ
/// </remarks>
/// <param name="connection">データベース接続</param>
/// <param name="logger">ロガー（オプショナル）</param>
public class UnitOfWork(IDbConnection connection, ILogger<UnitOfWork>? logger = null) : IUnitOfWork
{
    /// <inheritdoc />
    public async Task<T> CommandAsync<T>(Func<IUnitOfWorkContext, Task<T>> command)
    {
        // 二重スコープ検知
        if (UnitOfWorkScopeContext.Current.Value != null)
        {
            throw new InvalidOperationException(
                "Nested Command is not allowed. " +
                "An active transaction scope already exists in the current context.");
        }

        logger?.LogDebug("Starting Command (Write operation with transaction)");

        using var tx = connection.BeginTransaction();

        // スコープ開始をマーク
        UnitOfWorkScopeContext.Current.Value = this;

        try
        {
            var context = new UnitOfWorkContext(connection, tx);
            var result = await command(context);

            tx.Commit();
            logger?.LogInformation("Command completed successfully (Transaction committed)");

            return result;
        }
        catch (Exception ex)
        {
            tx.Rollback();
            logger?.LogWarning(ex, "Command failed (Transaction rolled back)");
            throw;
        }
        finally
        {
            // スコープ終了をマーク
            UnitOfWorkScopeContext.Current.Value = null;
        }
    }

    /// <inheritdoc />
    public async Task CommandAsync(Func<IUnitOfWorkContext, Task> command)
    {
        await CommandAsync<object?>(async ctx =>
        {
            await command(ctx);
            return null;
        });
    }

    /// <inheritdoc />
    public async Task<T> QueryAsync<T>(Func<IUnitOfWorkContext, Task<T>> query)
    {
        logger?.LogDebug("Starting Query (Read-only operation without transaction)");

        // Transaction に null を渡す（トランザクションなし）
        var context = new UnitOfWorkContext(connection, null);
        return await query(context);
    }
}