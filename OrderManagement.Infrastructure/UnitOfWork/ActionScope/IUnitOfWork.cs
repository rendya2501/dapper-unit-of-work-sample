namespace OrderManagement.Infrastructure.UnitOfWork.ActionScope;

/// <summary>
/// Unit of Work インターフェース（Action Scope パターン - CQRS 命名版）
/// </summary>
/// <remarks>
/// <para><strong>設計思想</strong></para>
/// <list type="bullet">
/// <item>トランザクション境界をスコープとして明示的に表現</item>
/// <item>Write 操作は CommandAsync 内で自動的にトランザクション管理</item>
/// <item>Read 操作は QueryAsync でトランザクションなし</item>
/// <item>スコープを抜けたら自動 Commit/Rollback</item>
/// </list>
/// </remarks>
public interface IUnitOfWork
{
    /// <summary>
    /// Command（書き込み）操作を実行します（自動トランザクション管理）
    /// </summary>
    /// <typeparam name="T">戻り値の型</typeparam>
    /// <param name="command">実行する処理</param>
    /// <returns>処理結果</returns>
    /// <remarks>
    /// <para>スコープ内で例外が発生した場合は自動的に Rollback されます。</para>
    /// <para>正常終了した場合は自動的に Commit されます。</para>
    /// </remarks>
    Task<T> CommandAsync<T>(Func<IUnitOfWorkContext, Task<T>> command);

    /// <summary>
    /// Command（書き込み）操作を実行します（戻り値なし）
    /// </summary>
    Task CommandAsync(Func<IUnitOfWorkContext, Task> command);

    /// <summary>
    /// Query（読み取り）操作を実行します（トランザクションなし）
    /// </summary>
    /// <typeparam name="T">戻り値の型</typeparam>
    /// <param name="query">実行する処理</param>
    /// <returns>処理結果</returns>
    /// <remarks>
    /// トランザクションを開始しないため、軽量な読み取り専用操作に使用します。
    /// </remarks>
    Task<T> QueryAsync<T>(Func<IUnitOfWorkContext, Task<T>> query);
}