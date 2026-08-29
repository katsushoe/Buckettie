namespace Buckettie.Application.Repositories;

/// <summary>
/// Buckettie内部Repository IDの契約です。
/// </summary>
public static class RepositoryId
{
    private const int MaximumLength = 128;

    /// <summary>
    /// IDがCredential targetにも安全に使用できる形式かを返します。
    /// </summary>
    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaximumLength
        && value[0] is >= 'a' and <= 'z'
        && value[1..].All(IsCanonicalCharacter);

    /// <summary>
    /// 検索用IDがCanonical IDの大文字小文字違いとして有効かを返します。
    /// </summary>
    public static bool IsLookupValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= MaximumLength
        && IsAsciiLetter(value[0])
        && value[1..].All(IsLookupCharacter);

    private static bool IsCanonicalCharacter(char character) =>
        character is >= 'a' and <= 'z'
        or >= '0' and <= '9';

    private static bool IsLookupCharacter(char character) =>
        IsAsciiLetter(character) || character is >= '0' and <= '9';

    private static bool IsAsciiLetter(char character) =>
        character is >= 'a' and <= 'z' or >= 'A' and <= 'Z';
}
