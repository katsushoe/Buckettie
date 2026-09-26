namespace Buckettie.Application.Git;

/// <summary>commitの作成者です。サービス実行アカウントのGit設定に依存しないよう明示して渡します。</summary>
/// <param name="Name">作成者名。</param>
/// <param name="Email">作成者メールアドレス。</param>
public sealed record GitCommitAuthor(string Name, string Email)
{
    private const int MaximumLength = 256;

    /// <summary>作成者名とメールアドレスが、Gitへ渡せる安全な値かを判定します。</summary>
    public static bool IsValid(string? name, string? email) =>
        IsValidName(name) && IsValidEmail(email);

    /// <summary>作成者名を検証します。Gitが拒否する山括弧と改行などの制御文字を許可しません。</summary>
    public static bool IsValidName(string? name) =>
        !string.IsNullOrWhiteSpace(name) && name.Length <= MaximumLength && name.Trim() == name
        && !name.Any(character => char.IsControl(character) || character is '<' or '>');

    /// <summary>メールアドレスを検証します。空白・制御文字・山括弧を含まず、@を1つだけ含む値に限ります。</summary>
    public static bool IsValidEmail(string? email) =>
        !string.IsNullOrWhiteSpace(email) && email.Length <= MaximumLength
        && email.Count(character => character == '@') == 1 && email[0] != '@' && email[^1] != '@'
        && !email.Any(character => char.IsControl(character) || char.IsWhiteSpace(character) || character is '<' or '>');
}
