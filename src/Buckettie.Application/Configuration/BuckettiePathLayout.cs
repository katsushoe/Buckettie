namespace Buckettie.Application.Configuration;

/// <summary>
/// Buckettieの標準インストール構成を表します。
/// </summary>
public sealed record BuckettiePathLayout(
    string InstallRoot,
    string BinaryDirectory,
    string ConfigurationDirectory,
    string LogDirectory,
    string DataDirectory,
    string SecretDirectory)
{
    /// <summary>
    /// バイナリディレクトリから標準インストール構成を解決します。
    /// </summary>
    public static BuckettiePathLayout FromBinaryDirectory(string binaryDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(binaryDirectory);

        string binary = Path.GetFullPath(binaryDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string? installRoot = Path.GetDirectoryName(binary);
        if (string.IsNullOrEmpty(installRoot))
        {
            throw new ArgumentException("Binary directory must have an install root.", nameof(binaryDirectory));
        }

        string data = Path.Combine(installRoot, "data");
        return new BuckettiePathLayout(
            installRoot,
            binary,
            Path.Combine(installRoot, "config"),
            Path.Combine(installRoot, "logs"),
            data,
            Path.Combine(data, "secrets"));
    }
}
