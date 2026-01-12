namespace OrderManagement.Infrastructure.UnitOfWork.ActionScope;


/// <summary>
/// スコープコンテキスト管理（二重スコープ検知用）
/// </summary>
internal static class UnitOfWorkScopeContext
{
    /// <summary>
    /// 現在アクティブな ExecuteAsync スコープ
    /// </summary>
    /// <remarks>
    /// AsyncLocal を使用することで、async/await を跨いでも
    /// 同じ非同期コンテキスト内で同じ値を共有できる
    /// </remarks>
    public static readonly AsyncLocal<UnitOfWork?> Current = new();
}
