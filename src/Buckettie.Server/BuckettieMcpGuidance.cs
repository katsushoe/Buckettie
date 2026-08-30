using System.ComponentModel;
using ModelContextProtocol.Server;

namespace Buckettie.Server;

/// <summary>Buckettie MCP Serverの利用案内を提供します。</summary>
public sealed class BuckettieMcpGuidance
{
    /// <summary>MCP初期化時にクライアントへ通知するServer Instructionsです。</summary>
    public const string ServerInstructions = """
        Buckettie is a Windows localhost gateway for safely operating explicitly registered local Git repositories and their Bitbucket Cloud repositories. It centralizes credentials, enforces repository and branch allowlists, and records audited operations so MCP clients do not need direct Bitbucket credentials.

        Use Buckettie's bitbucket_* tools for repositories managed by this server. Pass the configured Buckettie repository ID, not a filesystem path or an arbitrary remote URL. Before every push in every conversation, call list_projects and select the intended repository ID from its returned candidates. Inspect status, branches, pull requests, or tags before making changes. Use fetch to refresh remote-tracking refs; pull is fast-forward only. Push, pull-request creation or merge, tag creation, and repository registration changes mutate state and must only be called when the user's intent authorizes that exact operation. Protected-branch and repository policies are enforced by the server and must not be bypassed. Repository registration and policy updates require interactive approval on the host.

        Buckettieは、明示的に登録されたローカルGitリポジトリと対応するBitbucket Cloudリポジトリを安全に操作するWindows localhostゲートウェイです。認証情報を集約し、リポジトリとブランチのAllowlistを適用し、操作を監査記録します。管理対象リポジトリにはbitbucket_* Toolを使用し、ファイルパスや任意のRemote URLではなくBuckettieのリポジトリIDを渡してください。各会話でpushする前に必ずlist_projectsを呼び、返された候補から対象リポジトリIDを選択してください。変更前に状態を確認し、変更操作はユーザーがその操作を明示的に許可した場合だけ実行してください。
        """;

    /// <summary>Buckettieの目的と安全なTool利用方法を返します。</summary>
    [McpServerPrompt(Name = "buckettie_usage", Title = "Buckettie usage guide")]
    [Description("Buckettieの目的、安全境界、MCP Toolの使い方を示します。 / Explains Buckettie's purpose, security boundary, and MCP tool usage.")]
    public string GetUsageGuide() => ServerInstructions;
}
