namespace Buckettie.Application.Git;

/// <summary>
/// MCP呼び出しで指定されたGit Remote名（Moyaiの<c>gitRemoteName</c>）を、
/// 同じ非同期フロー内のGit操作へ引き渡します。
/// </summary>
/// <remarks>
/// 指定がない場合は<see langword="null"/>です。Git Gatewayは、この値、Repository登録のRemote名、
/// 自動解決の順にRemoteを決定します。
/// </remarks>
public static class GitRemoteSelection
{
    private static readonly AsyncLocal<string?> CurrentName = new();

    /// <summary>現在の非同期フローで指定されたRemote名です。</summary>
    public static string? Current => CurrentName.Value;

    /// <summary>破棄するまでの間、指定Remote名を現在の非同期フローへ設定します。</summary>
    public static IDisposable Use(string? remote)
    {
        string? previous = CurrentName.Value;
        CurrentName.Value = string.IsNullOrWhiteSpace(remote) ? null : remote.Trim();
        return new Scope(previous);
    }

    private sealed class Scope(string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            CurrentName.Value = previous;
            _disposed = true;
        }
    }
}
