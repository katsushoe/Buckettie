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
        && value.All(IsAllowedCharacter);

    private static bool IsAllowedCharacter(char character) =>
        character is >= 'a' and <= 'z'
        or >= 'A' and <= 'Z'
        or >= '0' and <= '9'
        or '.' or '_' or '-';
}
