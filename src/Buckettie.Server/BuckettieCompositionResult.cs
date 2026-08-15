using Buckettie.Application.Configuration;

namespace Buckettie.Server;

/// <summary>
/// Buckettie Serverのサービス構築結果を表します。
/// </summary>
public sealed record BuckettieCompositionResult(
    IServiceProvider? Services,
    IReadOnlyList<ConfigurationError> Errors) : IDisposable
{
    /// <summary>サービスが正常に構築されたかを示します。</summary>
    public bool IsSuccess => Services is not null && Errors.Count == 0;

    /// <inheritdoc />
    public void Dispose()
    {
        if (Services is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
