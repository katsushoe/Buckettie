namespace Buckettie.Server;

/// <summary>
/// Register/Unregister/UpdateがRepositoryAllowlistとIRepositoryStoreへ同時に書き込まないよう、
/// 全Repository変更操作で共有する単一Writer Gateです。
/// </summary>
internal sealed class RepositoryMutationGate : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <summary>他の変更操作が進行中でなければGateを獲得します。</summary>
    public Task<bool> TryEnterAsync(CancellationToken cancellationToken) =>
        _gate.WaitAsync(0, cancellationToken);

    /// <summary>Gateを解放します。</summary>
    public void Release() => _gate.Release();

    /// <inheritdoc />
    public void Dispose() => _gate.Dispose();
}
