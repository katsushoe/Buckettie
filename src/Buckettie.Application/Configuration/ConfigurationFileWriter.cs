namespace Buckettie.Application.Configuration;

/// <summary>
/// buckettie.jsonを安全に書き換えます。一時Fileへ書き出してからMoveすることで、
/// 書き込み途中の状態を本Fileへ残しません。
/// </summary>
public static class ConfigurationFileWriter
{
    /// <summary>
    /// 設定を一時Fileへ書き出してから本FileへatomicにMoveします。失敗時はfalseを返します。
    /// </summary>
    public static async Task<bool> SaveAtomicallyAsync(
        IBuckettieOptionsLoader loader,
        BuckettieOptions options,
        string configurationPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);

        string temporary = $"{configurationPath}.{Guid.NewGuid():N}.tmp";
        try
        {
            await using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                await loader.SaveAsync(options, stream, cancellationToken).ConfigureAwait(false);
            }

            File.Move(temporary, configurationPath, overwrite: true);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            TryDeleteTemporaryFile(temporary);
            return false;
        }
    }

    private static void TryDeleteTemporaryFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // 一時Fileの削除失敗は呼び出し元の結果に影響しないため無視します。
        }
    }
}
