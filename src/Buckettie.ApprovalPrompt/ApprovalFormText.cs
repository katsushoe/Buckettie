using System.Globalization;
using Buckettie.Application.Interactive;

namespace Buckettie.ApprovalPrompt;

/// <summary>承認Dialogの表示文字列です。</summary>
internal sealed record ApprovalFormText(
    string Title,
    string RepositoryId,
    string Workspace,
    string Slug,
    string LocalRoot,
    string RemoteUrl,
    string Token,
    string Approve,
    string Deny,
    string CountdownFormat)
{
    /// <summary>設定言語または指定UI Cultureに対応する表示文字列を返します。</summary>
    /// <param name="language">設定言語。</param>
    /// <param name="fallbackCulture">設定言語が自動の場合に使うUI Culture。</param>
    /// <param name="operation">利用者が承認する操作。Titleへ表示します。</param>
    public static ApprovalFormText ForLanguage(string language, CultureInfo fallbackCulture,
        ApprovalOperation operation = ApprovalOperation.Register)
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
                operation switch
                {
                    ApprovalOperation.Update => "Buckettie - リポジトリ設定変更の承認",
                    ApprovalOperation.Unregister => "Buckettie - リポジトリ登録解除の承認",
                    _ => "Buckettie - リポジトリ登録の承認",
                },
                "リポジトリID",
                "ワークスペース",
                "スラッグ",
                "ローカルルート",
                "リモートURL",
                "API Token",
                "承認(&A)",
                "拒否(&D)",
                "応答がない場合、{0}秒後に自動的に拒否します。")
            : new(
                operation switch
                {
                    ApprovalOperation.Update => "Buckettie - Approve Repository Policy Update",
                    ApprovalOperation.Unregister => "Buckettie - Approve Repository Unregistration",
                    _ => "Buckettie - Approve Repository Registration",
                },
                "Repository ID",
                "Workspace",
                "Slug",
                "Local Root",
                "Remote URL",
                "API Token",
                "&Approve",
                "&Deny",
                "Auto-deny in {0}s if no response.");
    }
}
