namespace Buckettie.Application.Bitbucket;

/// <summary>明示作成元の入力規則です。40桁SHA以外はBranch名として扱います。</summary>
public static class BranchSource
{
    /// <summary>完全なSHA-1コミットIDかを判定します。</summary>
    public static bool IsCommit(string? source) =>
        source is { Length: 40 } && source.All(Uri.IsHexDigit);

    /// <summary>任意のGit式を許可せず、Branch名または完全SHAだけを受け付けます。</summary>
    public static bool IsValid(string? source)
    {
        if (string.IsNullOrWhiteSpace(source) || source.Length > 255
            || source.Any(character => char.IsWhiteSpace(character) || char.IsControl(character))
            || source.IndexOfAny(['~', '^', ':', '?', '*', '[', '\\']) >= 0
            || source.Contains("..", StringComparison.Ordinal)
            || source.Contains("@{", StringComparison.Ordinal)
            || source.StartsWith('-') || source.EndsWith('.') || source is "@" or "HEAD")
        {
            return false;
        }

        return source.Split('/').All(part => part.Length > 0 && !part.StartsWith('.')
            && !part.EndsWith(".lock", StringComparison.Ordinal));
    }
}
