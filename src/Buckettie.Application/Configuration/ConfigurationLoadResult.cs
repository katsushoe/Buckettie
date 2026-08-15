namespace Buckettie.Application.Configuration;

/// <summary>
/// 設定読込時のエラーコードです。
/// </summary>
public enum ConfigurationErrorCode
{
    InvalidJson,
    DuplicateRepositoryId,
    InvalidRepositoryId,
    RequiredValueMissing,
    InvalidTagPattern,
}

/// <summary>
/// 設定読込時の検証エラーを表します。
/// </summary>
public sealed record ConfigurationError(ConfigurationErrorCode Code, string Path);

/// <summary>
/// 設定読込結果を表します。
/// </summary>
public sealed record ConfigurationLoadResult(
    BuckettieOptions? Options,
    IReadOnlyList<ConfigurationError> Errors)
{
    /// <summary>設定が正常に読み込まれたかを示します。</summary>
    public bool IsValid => Options is not null && Errors.Count == 0;

    /// <summary>成功結果を生成します。</summary>
    public static ConfigurationLoadResult Success(BuckettieOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new(options, Array.Empty<ConfigurationError>());
    }

    /// <summary>失敗結果を生成します。</summary>
    public static ConfigurationLoadResult Failure(params ConfigurationError[] errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return new(null, errors);
    }
}
