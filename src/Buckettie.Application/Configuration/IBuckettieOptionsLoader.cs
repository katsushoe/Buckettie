namespace Buckettie.Application.Configuration;

/// <summary>
/// Buckettie設定を読み込み、起動前検証を行います。
/// </summary>
public interface IBuckettieOptionsLoader
{
    /// <summary>
    /// JSON Streamから設定を非同期で読み込みます。
    /// </summary>
    public Task<ConfigurationLoadResult> LoadAsync(
        Stream json,
        CancellationToken cancellationToken = default);
}
