namespace Buckettie.Application.Configuration;

/// <summary>
/// Bitbucket Cloud usernameを検証します。
/// </summary>
public static class BitbucketUsername
{
    private const int MaximumLength = 100;

    /// <summary>Git HTTPS認証に使用できるusernameかを返します。</summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaximumLength
        && value.All(character => char.IsLetterOrDigit(character) || character is '_' or '-');
}
