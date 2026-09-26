using Moyai.ProviderAuthentication;

namespace Buckettie.Server;

/// <summary>Toolから最小Scopeを決定します。ClientからScopeを受け取りません。</summary>
public static class ProviderToolPolicy
{
    /// <summary>Project情報を返さないBootstrap Toolです。</summary>
    public static bool IsBootstrap(string tool) => tool is "get_version" or "bitbucket_provider_capabilities";

    /// <summary>別管理者認証を必要とするToolです。</summary>
    public static bool IsAdministrator(string tool) => tool is "list_projects" or "bitbucket_repository_register"
        or "bitbucket_repository_unregister" or "bitbucket_repository_update";

    /// <summary>loopback直接接続でAssertionなしに許可する読み取りToolです。</summary>
    /// <remarks>Scope Mappingが<c>repository.read</c>だけのToolとProvider登録一覧に限ります。</remarks>
    public static bool IsDirectRead(string tool) => tool == "list_projects" || Scopes(tool) is ["repository.read"];

    /// <summary>loopback直接接続から、対話Desktop承認を条件に許可する管理Toolです。</summary>
    public static bool IsDirectAdministration(string tool) => tool is "bitbucket_repository_register"
        or "bitbucket_repository_unregister" or "bitbucket_repository_update";

    /// <summary>共通契約またはProvider固有Scopeを返します。</summary>
    public static string[]? Scopes(string tool) => tool switch
    {
        "bitbucket_repository_status" or "bitbucket_repository_diff" or "bitbucket_branch_list"
            or "bitbucket_branch_get" or "bitbucket_tag_list" or "bitbucket_tag_get"
            or "bitbucket_pr_list" or "bitbucket_pr_get" or "bitbucket_pr_diff"
            or "buckettie_release_get" or "bitbucket_history_rewrite_preview" => ["repository.read"],
        "bitbucket_repository_commit" => ["repository.commit"],
        "bitbucket_push" => ["repository.push"],
        "bitbucket_pull" => ["repository.pull"],
        "bitbucket_branch_create" or "bitbucket_branch_delete" => ["repository.branch.write"],
        "bitbucket_tag_create" or "bitbucket_tag_delete" or "bitbucket_tag_push" => ["repository.tag.write"],
        "buckettie_release_create" => ["release.publish"],
        "buckettie_release_publish" => ["release.publish", "artifact.upload"],
        "bitbucket_fetch" => ["provider.buckettie.fetch"],
        "bitbucket_pr_create" => ["provider.buckettie.pr_create"],
        "bitbucket_pr_merge" => ["provider.buckettie.pr_merge"],
        "bitbucket_history_rewrite_execute" => ["provider.buckettie.history_rewrite"],
        "bitbucket_force_push_with_lease" => ["provider.buckettie.force_push_with_lease"],
        "buckettie_release_withdraw" => ["provider.buckettie.release_withdraw"],
        _ => null,
    };

    /// <summary>実際に公開するToolだけからCapabilityを構成します。</summary>
    public static AssertionCapability Capability { get; } = new("buckettie", "buckettie", "1", "ES256", true,
        typeof(BuckettieMcpTools).GetMethods()
            .Select(method => method.GetCustomAttributes(typeof(ModelContextProtocol.Server.McpServerToolAttribute), false)
                .Cast<ModelContextProtocol.Server.McpServerToolAttribute>().SingleOrDefault()?.Name)
            .Where(name => name is not null && Scopes(name) is not null)
            .ToDictionary(name => name!, name => Scopes(name!)!, StringComparer.Ordinal));
}
