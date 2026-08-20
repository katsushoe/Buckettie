using System.ComponentModel;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Configuration;
using Buckettie.Application.Git;
using ModelContextProtocol.Server;

namespace Buckettie.Server;

/// <summary>Buckettieが公開する固定MCP Toolです。</summary>
public sealed class BuckettieMcpTools
{
    private static readonly string ProductVersion =
        typeof(BuckettieMcpTools).Assembly.GetName().Version?.ToString() ?? "unknown";

    private readonly IBitbucketRepositoryGateway _bitbucket;
    private readonly IGitGateway _git;
    private readonly IRepositoryRegistrationService _registration;
    private readonly IRepositoryUnregistrationService _unregistration;
    private readonly IRepositoryUpdateService _update;
    private readonly string _language;

    /// <summary>MCP Toolを初期化します。</summary>
    public BuckettieMcpTools(
        IGitGateway git,
        IBitbucketRepositoryGateway bitbucket,
        IRepositoryRegistrationService registration,
        IRepositoryUnregistrationService unregistration,
        IRepositoryUpdateService update,
        BuckettieOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(git);
        ArgumentNullException.ThrowIfNull(bitbucket);
        ArgumentNullException.ThrowIfNull(registration);
        ArgumentNullException.ThrowIfNull(unregistration);
        ArgumentNullException.ThrowIfNull(update);
        _git = git;
        _bitbucket = bitbucket;
        _registration = registration;
        _unregistration = unregistration;
        _update = update;
        _language = options?.Language ?? "en-US";
    }

    /// <summary>稼働中のBuckettieバージョンを取得します。</summary>
    [McpServerTool(Name = "get_version", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("Returns the running Buckettie version.")]
    public Task<BuckettieToolResult<BuckettieVersionData>> GetVersionAsync() =>
        Task.FromResult(new BuckettieToolResult<BuckettieVersionData>(
            true, "get_version", string.Empty, new BuckettieVersionData(ProductVersion), null));

    /// <summary>Repositoryのローカル状態を取得します。</summary>
    [McpServerTool(Name = "bitbucket_repository_status", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Returns the configured repository's local branch, HEAD, and working-tree status.")]
    public Task<BuckettieToolResult<BuckettieGitData>> RepositoryStatusAsync(
        [Description("Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapGitAsync(_git.GetStatusAsync(repository, cancellationToken), _language);

    /// <summary>設定済みRemoteからfetchします。</summary>
    [McpServerTool(Name = "bitbucket_fetch", Destructive = false, Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Fetches refs from the repository's configured Bitbucket remote.")]
    public Task<BuckettieToolResult<BuckettieGitData>> FetchAsync(
        [Description("Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapGitAsync(_git.FetchAsync(repository, cancellationToken), _language);

    /// <summary>現在Branchをfast-forward限定でpullします。</summary>
    [McpServerTool(Name = "bitbucket_pull", Destructive = false, Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Pulls the current allowed branch using fast-forward only.")]
    public Task<BuckettieToolResult<BuckettieGitData>> PullAsync(
        [Description("Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapGitAsync(_git.PullAsync(repository, cancellationToken), _language);

    /// <summary>現在の許可Branchをpushします。</summary>
    [McpServerTool(Name = "bitbucket_push", Destructive = true, Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Pushes the current branch after applying repository and protected-branch policies.")]
    public Task<BuckettieToolResult<BuckettieGitData>> PushAsync(
        [Description("Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapGitAsync(_git.PushAsync(repository, cancellationToken), _language);

    /// <summary>Remote Branch一覧を取得します。</summary>
    [McpServerTool(Name = "bitbucket_branch_list", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Lists Bitbucket branches for an allowed repository.")]
    public Task<BuckettieToolResult<IReadOnlyList<BitbucketBranchInfo>>> ListBranchesAsync(
        [Description("Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.ListBranchesAsync(repository, cancellationToken), "branch_list", repository, _language);

    /// <summary>Remote Branch詳細を取得します。</summary>
    [McpServerTool(Name = "bitbucket_branch_get", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Gets a Bitbucket branch and its target commit hash.")]
    public Task<BuckettieToolResult<BitbucketBranchInfo>> GetBranchAsync(
        [Description("Buckettie repository ID.")] string repository,
        [Description("Branch name.")] string branch,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.GetBranchAsync(repository, branch, cancellationToken), "branch_get", repository, _language);

    /// <summary>Pull Request一覧を取得します。</summary>
    [McpServerTool(Name = "bitbucket_pr_list", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Lists Bitbucket pull requests, optionally filtered by state.")]
    public Task<BuckettieToolResult<IReadOnlyList<BitbucketPullRequestInfo>>> ListPullRequestsAsync(
        [Description("Buckettie repository ID.")] string repository,
        [Description("Optional pull-request state.")] BitbucketPullRequestState? state = null,
        [Description("Optional exact source branch filter.")] string? source = null,
        [Description("Optional exact destination branch filter.")] string? destination = null,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.ListPullRequestsAsync(repository, state, source, destination, cancellationToken),
            "pr_list",
            repository,
            _language);

    /// <summary>Pull Request詳細を取得します。</summary>
    [McpServerTool(Name = "bitbucket_pr_get", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Gets a Bitbucket pull request by ID.")]
    public Task<BuckettieToolResult<BitbucketPullRequestInfo>> GetPullRequestAsync(
        [Description("Buckettie repository ID.")] string repository,
        [Description("Pull-request ID.")] int pullRequestId,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.GetPullRequestAsync(repository, pullRequestId, cancellationToken), "pr_get", repository, _language);

    /// <summary>Pull Request diffを取得します。</summary>
    [McpServerTool(Name = "bitbucket_pr_diff", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Gets the bounded unified diff for a Bitbucket pull request.")]
    public Task<BuckettieToolResult<string>> GetPullRequestDiffAsync(
        [Description("Buckettie repository ID.")] string repository,
        [Description("Pull-request ID.")] int pullRequestId,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.GetPullRequestDiffAsync(repository, pullRequestId, cancellationToken), "pr_diff", repository, _language);

    /// <summary>設定済みdevelopからmainへのPull Requestを作成します。</summary>
    [McpServerTool(Name = "bitbucket_pr_create", Destructive = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Creates a pull request using the repository's configured develop-to-main route.")]
    public Task<BuckettieToolResult<BitbucketPullRequestInfo>> CreatePullRequestAsync(
        [Description("Buckettie repository ID.")] string repository,
        [Description("Pull-request title.")] string title,
        [Description("Pull-request description.")] string description,
        [Description("Whether to create the pull request as a draft.")] bool draft = false,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.CreatePullRequestAsync(
                repository,
                new BitbucketPullRequestCreate(title, description, draft),
                cancellationToken),
            "pr_create",
            repository,
            _language);

    /// <summary>Policy検証後にPull Requestをmergeします。</summary>
    [McpServerTool(Name = "bitbucket_pr_merge", Destructive = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Merges an OPEN pull request only when it follows the configured develop-to-main route.")]
    public Task<BuckettieToolResult<BitbucketPullRequestInfo>> MergePullRequestAsync(
        [Description("Buckettie repository ID.")] string repository,
        [Description("Pull-request ID.")] int pullRequestId,
        [Description("Merge strategy; RepositoryDefault uses the repository setting.")]
        BitbucketMergeStrategy strategy = BitbucketMergeStrategy.RepositoryDefault,
        [Description("Optional merge commit message.")] string? message = null,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.MergePullRequestAsync(
                repository,
                pullRequestId,
                new BitbucketPullRequestMerge(strategy, message),
                cancellationToken),
            "pr_merge",
            repository,
            _language);

    /// <summary>Tag一覧を取得します。</summary>
    [McpServerTool(Name = "bitbucket_tag_list", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Lists Bitbucket tags for an allowed repository.")]
    public Task<BuckettieToolResult<IReadOnlyList<BitbucketTagInfo>>> ListTagsAsync(
        [Description("Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.ListTagsAsync(repository, cancellationToken), "tag_list", repository, _language);

    /// <summary>Tag詳細を取得します。</summary>
    [McpServerTool(Name = "bitbucket_tag_get", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Gets a Bitbucket tag and its target commit hash.")]
    public Task<BuckettieToolResult<BitbucketTagInfo>> GetTagAsync(
        [Description("Buckettie repository ID.")] string repository,
        [Description("Tag name.")] string tag,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.GetTagAsync(repository, tag, cancellationToken), "tag_get", repository, _language);

    /// <summary>設定済み対象BranchのHEADへTagを作成します。</summary>
    [McpServerTool(Name = "bitbucket_tag_create", Destructive = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Creates a policy-compliant tag at the configured target branch's current HEAD.")]
    public Task<BuckettieToolResult<BitbucketTagInfo>> CreateTagAsync(
        [Description("Buckettie repository ID.")] string repository,
        [Description("Policy-compliant tag name.")] string tag,
        [Description("Optional annotated-tag message.")] string? message = null,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.CreateTagAsync(repository, new BitbucketTagCreate(tag, message), cancellationToken),
            "tag_create",
            repository,
            _language);

    /// <summary>新規RepositoryをAllowlistへ登録します。対話Desktopでの人間承認が必須です。</summary>
    [McpServerTool(Name = "bitbucket_repository_register", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = true, UseStructuredContent = true)]
    [Description("Proposes registering a new repository in the allowlist; requires interactive human approval " +
        "on the server's desktop session. Workspace/Slug are always derived from the local Git remote, never " +
        "from caller input, and branch policy fields are server-defaulted.")]
    public async Task<BuckettieToolResult<BuckettieRepositoryRegistrationData>> RegisterRepositoryAsync(
        [Description("New Buckettie repository ID to register.")] string repository,
        [Description("Absolute local path of the existing Git repository to register.")] string localRoot,
        [Description("Git remote name to validate and use.")] string remote = "origin",
        [Description("Development branch name.")] string developBranch = "develop",
        [Description("Main branch name.")] string mainBranch = "main",
        CancellationToken cancellationToken = default)
    {
        RepositoryRegistrationOutcome outcome = await _registration.RegisterAsync(
            repository, localRoot, remote, developBranch, mainBranch, cancellationToken).ConfigureAwait(false);
        return outcome.IsSuccess
            ? new(true, "bitbucket_repository_register", repository,
                new BuckettieRepositoryRegistrationData(
                    outcome.RepositoryId!, outcome.Workspace!, outcome.Slug!, true),
                null)
            : new(false, "bitbucket_repository_register", repository, null,
                BuckettieToolResultMapper.Localize(outcome.Error!, _language));
    }

    /// <summary>登録済みRepositoryをAllowlistから削除します。Push/PR/Tag権限を削減するだけの操作のため、承認は不要です。</summary>
    [McpServerTool(Name = "bitbucket_repository_unregister", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Removes a registered repository from the allowlist. Since this only revokes push/PR/tag " +
        "rights, no interactive approval is required.")]
    public async Task<BuckettieToolResult<BuckettieRepositoryUnregistrationData>> UnregisterRepositoryAsync(
        [Description("Buckettie repository ID to unregister.")] string repository,
        CancellationToken cancellationToken = default)
    {
        RepositoryUnregistrationOutcome outcome = await _unregistration
            .UnregisterAsync(repository, cancellationToken).ConfigureAwait(false);
        return outcome.IsSuccess
            ? new(true, "bitbucket_repository_unregister", repository,
                new BuckettieRepositoryUnregistrationData(outcome.RepositoryId!), null)
            : new(false, "bitbucket_repository_unregister", repository, null,
                BuckettieToolResultMapper.Localize(outcome.Error!, _language));
    }

    /// <summary>登録済みRepositoryのBranch Policyを修正します。対話Desktopでの人間承認が必須です。</summary>
    [McpServerTool(Name = "bitbucket_repository_update", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("Proposes updating a registered repository's branch policy (direct-push/pull/protected " +
        "branches, tag target/pattern, require-clean-working-tree); requires interactive human approval on " +
        "the server's desktop session. Workspace/Slug/LocalRoot/Remote/DevelopBranch/MainBranch cannot be " +
        "changed here — unregister and re-register instead.")]
    public async Task<BuckettieToolResult<BuckettieRepositoryUpdateData>> UpdateRepositoryAsync(
        [Description("Buckettie repository ID to update.")] string repository,
        [Description("Branches allowed to push directly.")] HashSet<string> directPushBranches,
        [Description("Branches allowed to be pulled.")] HashSet<string> pullBranches,
        [Description("Branches that are protected from direct push.")] HashSet<string> protectedBranches,
        [Description("Branch that release tags target.")] string tagTargetBranch,
        [Description("Regular expression allowed release tag names must match.")] string tagPattern,
        [Description("Whether push requires a clean working tree.")] bool requireCleanWorkingTree = true,
        CancellationToken cancellationToken = default)
    {
        RepositoryUpdateRequest request = new(
            directPushBranches, pullBranches, protectedBranches, tagTargetBranch, tagPattern,
            requireCleanWorkingTree);
        RepositoryUpdateOutcome outcome = await _update
            .UpdateAsync(repository, request, cancellationToken).ConfigureAwait(false);
        return outcome.IsSuccess
            ? new(true, "bitbucket_repository_update", repository,
                new BuckettieRepositoryUpdateData(outcome.RepositoryId!, true), null)
            : new(false, "bitbucket_repository_update", repository, null,
                BuckettieToolResultMapper.Localize(outcome.Error!, _language));
    }
}
