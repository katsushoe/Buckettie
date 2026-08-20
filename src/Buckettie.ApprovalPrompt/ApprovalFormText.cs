using System.Globalization;

namespace Buckettie.ApprovalPrompt;

/// <summary>承認Dialogの表示文字列です。</summary>
internal sealed record ApprovalFormText(
    string Title,
    string RepositoryId,
    string Workspace,
    string Slug,
    string LocalRoot,
    string RemoteUrl,
    string Approve,
    string Deny,
    string CountdownFormat)
{
    /// <summary>設定言語または指定UI Cultureに対応する表示文字列を返します。</summary>
    public static ApprovalFormText ForLanguage(string language, CultureInfo fallbackCulture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        ArgumentNullException.ThrowIfNull(fallbackCulture);
        bool useJapanese = language switch
        {
            "ja-JP" => true,
            "en-US" => false,
            _ => string.Equals(
                fallbackCulture.TwoLetterISOLanguageName, "ja", StringComparison.OrdinalIgnoreCase),
        };
        return useJapanese
            ? new(
                "Buckettie - リポジトリ操作の承認",
                "リポジトリID",
                "ワークスペース",
                "スラッグ",
                "ローカルルート",
                "リモートURL",
                "承認(&A)",
                "拒否(&D)",
                "応答がない場合、{0}秒後に自動的に拒否します。")
            : new(
                "Buckettie - Repository Operation Approval",
                "Repository ID",
                "Workspace",
                "Slug",
                "Local Root",
                "Remote URL",
                "&Approve",
                "&Deny",
                "Auto-deny in {0}s if no response.");
    }
}
