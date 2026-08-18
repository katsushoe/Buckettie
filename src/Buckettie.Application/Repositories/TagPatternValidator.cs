using System.Text.RegularExpressions;

namespace Buckettie.Application.Repositories;

/// <summary>
/// Repository設定のtag_patternが有効な正規表現かを検証します。
/// </summary>
public static class TagPatternValidator
{
    /// <summary>指定した正規表現がCultureInvariantでCompileできるかを返します。</summary>
    public static bool IsValid(string tagPattern)
    {
        if (string.IsNullOrWhiteSpace(tagPattern))
        {
            return false;
        }

        try
        {
            _ = new Regex(tagPattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
