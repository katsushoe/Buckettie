using System.Globalization;

namespace Buckettie.Application.Configuration;

/// <summary>設定された表示言語を解決します。</summary>
public static class BuckettieLanguage
{
    /// <summary>日本語表示を使用するか判定します。</summary>
    public static bool IsJapanese(string? language, CultureInfo? fallbackCulture = null)
    {
        if (string.Equals(language, "ja-JP", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(language, "en-US", StringComparison.OrdinalIgnoreCase)) return false;
        return (fallbackCulture ?? CultureInfo.CurrentUICulture).Name.StartsWith("ja", StringComparison.OrdinalIgnoreCase);
    }
}
