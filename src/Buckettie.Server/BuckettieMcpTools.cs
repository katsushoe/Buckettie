using System.ComponentModel;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Configuration;
using Buckettie.Application.Git;
using Buckettie.Application.Repositories;
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
    private readonly RepositoryAllowlist? _allowlist;
    private readonly string _language;

    /// <summary>MCP Toolを初期化します。</summary>
    public BuckettieMcpTools(
        IGitGateway git,
        IBitbucketRepositoryGateway bitbucket,
        IRepositoryRegistrationService registration,
        IRepositoryUnregistrationService unregistration,
        IRepositoryUpdateService update,
        BuckettieOptions? options = null,
        RepositoryAllowlist? allowlist = null)
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
        _allowlist = allowlist;
        _language = options?.Language ?? "en-US";
    }

    /// <summary>稼働中のBuckettieバージョンを取得します。</summary>
    [McpServerTool(Name = "get_version", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("稼働中のBuckettieバージョンを返します。 / Returns the running Buckettie version.")]
    public Task<BuckettieToolResult<BuckettieVersionData>> GetVersionAsync() =>
        Task.FromResult(new BuckettieToolResult<BuckettieVersionData>(
            true, "get_version", string.Empty, new BuckettieVersionData(ProductVersion), null));

    /// <summary>登録済みProject IDを一覧します。</summary>
    [McpServerTool(Name = "list_projects", ReadOnly = true, Destructive = false, Idempotent = true,
        OpenWorld = false, UseStructuredContent = true)]
    [Description("登録済みBuckettieプロジェクトIDを一覧し、操作対象名の候補を返します。 / Lists registered Buckettie project IDs as candidate names for operations.")]
    public Task<BuckettieToolResult<BuckettieProjectListData>> ListProjectsAsync()
    {
        IReadOnlyList<string> projects = _allowlist?.ListIds() ?? [];
        return Task.FromResult(new BuckettieToolResult<BuckettieProjectListData>(
            true, "list_projects", string.Empty, new BuckettieProjectListData(projects), null));
    }

    /// <summary>Repository Contractで利用可能な操作を返します。</summary>
    [McpServerTool(Name = "bitbucket_provider_capabilities", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("Repository Contractの各操作についてBitbucket Providerの対応可否を返します。 / Returns support flags for Bitbucket Repository Contract operations.")]
    public Task<BuckettieToolResult<BitbucketProviderCapabilities>> GetProviderCapabilitiesAsync()
    {
        Dictionary<string, bool> operations = new(StringComparer.Ordinal)
        {
            ["branch_create"] = true,
            ["branch_delete"] = true,
            ["tag_delete"] = true,
            ["tag_push"] = true,
            ["explicit_push"] = true,
            ["repository_diff"] = true,
            ["repository_commit"] = true,
        };
        return Task.FromResult(new BuckettieToolResult<BitbucketProviderCapabilities>(
            true,
            "provider_capabilities",
            string.Empty,
            new BitbucketProviderCapabilities("bitbucket", operations),
            null));
    }

    /// <summary>Repositoryのローカル状態を取得します。</summary>
    [McpServerTool(Name = "bitbucket_repository_status", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("設定済みリポジトリのローカルブランチ、HEAD、作業ツリー状態を返します。 / Returns the configured repository's local branch, HEAD, and working-tree status.")]
    public Task<BuckettieToolResult<BuckettieGitData>> RepositoryStatusAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapGitAsync(_git.GetStatusAsync(repository, cancellationToken), _language);

    /// <summary>RepositoryのHEADに対する作業ツリー差分を取得します。</summary>
    [McpServerTool(Name = "bitbucket_repository_diff", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = false, UseStructuredContent = true)]
    [Description("登録済みリポジトリのHEADに対する作業ツリー差分を返します。 / Returns the working-tree diff against HEAD for an allowed repository.")]
    public Task<BuckettieToolResult<BuckettieGitData>> RepositoryDiffAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapGitAsync(_git.GetDiffAsync(repository, cancellationToken), _language);

    /// <summary>Policyに従って作業ツリーの変更をlocal commitします。</summary>
    [McpServerTool(Name = "bitbucket_repository_commit", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = false, UseStructuredContent = true)]
    [Description("許可された現在ブランチで作業ツリーの変更をすべてlocal commitします。 / Commits all working-tree changes locally on the current policy-allowed branch.")]
    public Task<BuckettieToolResult<BuckettieGitData>> RepositoryCommitAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("commitメッセージ。 / Commit message.")] string message,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapGitAsync(
            _git.CommitAsync(repository, message, cancellationToken), _language);

    /// <summary>設定済みRemoteからfetchします。</summary>
    [McpServerTool(Name = "bitbucket_fetch", Destructive = false, Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("設定済みBitbucketリモートからrefを取得します。 / Fetches refs from the repository's configured Bitbucket remote.")]
    public Task<BuckettieToolResult<BuckettieGitData>> FetchAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapGitAsync(_git.FetchAsync(repository, cancellationToken), _language);

    /// <summary>現在Branchをfast-forward限定でpullします。</summary>
    [McpServerTool(Name = "bitbucket_pull", Destructive = false, Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("許可された現在のブランチをfast-forward限定でpullします。 / Pulls the current allowed branch using fast-forward only.")]
    public Task<BuckettieToolResult<BuckettieGitData>> PullAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapGitAsync(_git.PullAsync(repository, cancellationToken), _language);

    /// <summary>現在の許可Branchをpushします。</summary>
    [McpServerTool(Name = "bitbucket_push", Destructive = true, Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("リポジトリと保護ブランチのポリシーを適用して現在のブランチをpushします。 / Pushes the current branch after applying repository and protected-branch policies.")]
    public Task<BuckettieToolResult<BuckettieGitData>> PushAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapGitAsync(
            _git.PushAsync(repository, cancellationToken), _language, _allowlist?.ListIds());

    /// <summary>Remote Branch一覧を取得します。</summary>
    [McpServerTool(Name = "bitbucket_branch_list", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("許可されたリポジトリのBitbucketブランチを一覧表示します。 / Lists Bitbucket branches for an allowed repository.")]
    public Task<BuckettieToolResult<IReadOnlyList<BitbucketBranchInfo>>> ListBranchesAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.ListBranchesAsync(repository, cancellationToken), "branch_list", repository, _language);

    /// <summary>Remote Branch詳細を取得します。</summary>
    [McpServerTool(Name = "bitbucket_branch_get", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Bitbucketブランチと対象コミットハッシュを取得します。 / Gets a Bitbucket branch and its target commit hash.")]
    public Task<BuckettieToolResult<BitbucketBranchInfo>> GetBranchAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("ブランチ名。 / Branch name.")] string branch,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.GetBranchAsync(repository, branch, cancellationToken), "branch_get", repository, _language);

    /// <summary>明示した作成元からRemote Branchを作成します。ローカル切替は行いません。</summary>
    [McpServerTool(Name = "bitbucket_branch_create", Destructive = true, Idempotent = false,
        OpenWorld = true, UseStructuredContent = true)]
    [Description("明示した作成元からBitbucketブランチを作成します。省略・暗黙補完・ローカル切替はありません。 / Creates a remote branch from an explicit source branch or full commit SHA; no default source or local checkout.")]
    public Task<BuckettieToolResult<BitbucketBranchInfo>> CreateBranchAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("作成するブランチ名。 / Branch name to create.")] string branch,
        [Description("必須の作成元Branch名または完全40桁コミットSHA。 / Required source branch name or full 40-character commit SHA.")] string source,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.CreateBranchAsync(repository, branch, source, cancellationToken),
            "branch_create", repository, _language);

    /// <summary>保護規則を適用してRemote Branchを削除します。</summary>
    [McpServerTool(Name = "bitbucket_branch_delete", Destructive = true, Idempotent = false,
        OpenWorld = true, UseStructuredContent = true)]
    [Description("develop、main、保護ブランチを除くBitbucketブランチを削除します。 / Deletes a Bitbucket branch except develop, main, and protected branches.")]
    public Task<BuckettieToolResult<bool>> DeleteBranchAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("削除するブランチ名。 / Branch name to delete.")] string branch,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.DeleteBranchAsync(repository, branch, cancellationToken),
            "branch_delete", repository, _language);

    /// <summary>Pull Request一覧を取得します。</summary>
    [McpServerTool(Name = "bitbucket_pr_list", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Bitbucketプルリクエストを一覧表示します。状態による絞り込みが可能です。 / Lists Bitbucket pull requests, optionally filtered by state.")]
    public Task<BuckettieToolResult<IReadOnlyList<BitbucketPullRequestInfo>>> ListPullRequestsAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("任意のプルリクエスト状態。 / Optional pull-request state.")] BitbucketPullRequestState? state = null,
        [Description("任意の完全一致ソースブランチフィルター。 / Optional exact source branch filter.")] string? source = null,
        [Description("任意の完全一致宛先ブランチフィルター。 / Optional exact destination branch filter.")] string? destination = null,
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
    [Description("IDを指定してBitbucketプルリクエストを取得します。 / Gets a Bitbucket pull request by ID.")]
    public Task<BuckettieToolResult<BitbucketPullRequestInfo>> GetPullRequestAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("プルリクエストID。 / Pull-request ID.")] int pullRequestId,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.GetPullRequestAsync(repository, pullRequestId, cancellationToken), "pr_get", repository, _language);

    /// <summary>Pull Request diffを取得します。</summary>
    [McpServerTool(Name = "bitbucket_pr_diff", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Bitbucketプルリクエストの制限付きunified diffを取得します。 / Gets the bounded unified diff for a Bitbucket pull request.")]
    public Task<BuckettieToolResult<string>> GetPullRequestDiffAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("プルリクエストID。 / Pull-request ID.")] int pullRequestId,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.GetPullRequestDiffAsync(repository, pullRequestId, cancellationToken), "pr_diff", repository, _language);

    /// <summary>設定済みdevelopからmainへのPull Requestを作成します。</summary>
    [McpServerTool(Name = "bitbucket_pr_create", Destructive = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("設定済みdevelopからmainへの経路でプルリクエストを作成します。 / Creates a pull request using the repository's configured develop-to-main route.")]
    public Task<BuckettieToolResult<BitbucketPullRequestInfo>> CreatePullRequestAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("プルリクエストのタイトル。 / Pull-request title.")] string title,
        [Description("プルリクエストの説明。 / Pull-request description.")] string description,
        [Description("ドラフトとして作成するか。 / Whether to create the pull request as a draft.")] bool draft = false,
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
    [Description("設定済みdevelopからmainへの経路に従うOPEN状態のプルリクエストをマージします。 / Merges an OPEN pull request only when it follows the configured develop-to-main route.")]
    public Task<BuckettieToolResult<BitbucketPullRequestInfo>> MergePullRequestAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("プルリクエストID。 / Pull-request ID.")] int pullRequestId,
        [Description("マージ方式。RepositoryDefaultはリポジトリ設定を使用します。 / Merge strategy; RepositoryDefault uses the repository setting.")]
        BitbucketMergeStrategy strategy = BitbucketMergeStrategy.RepositoryDefault,
        [Description("任意のマージコミットメッセージ。 / Optional merge commit message.")] string? message = null,
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
    [Description("許可されたリポジトリのBitbucketタグを一覧表示します。 / Lists Bitbucket tags for an allowed repository.")]
    public Task<BuckettieToolResult<IReadOnlyList<BitbucketTagInfo>>> ListTagsAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.ListTagsAsync(repository, cancellationToken), "tag_list", repository, _language);

    /// <summary>Tag詳細を取得します。</summary>
    [McpServerTool(Name = "bitbucket_tag_get", ReadOnly = true, Destructive = false,
        Idempotent = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("Bitbucketタグと対象コミットハッシュを取得します。 / Gets a Bitbucket tag and its target commit hash.")]
    public Task<BuckettieToolResult<BitbucketTagInfo>> GetTagAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("タグ名。 / Tag name.")] string tag,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.GetTagAsync(repository, tag, cancellationToken), "tag_get", repository, _language);

    /// <summary>設定済み対象BranchのHEADへTagを作成します。</summary>
    [McpServerTool(Name = "bitbucket_tag_create", Destructive = true, OpenWorld = true,
        UseStructuredContent = true)]
    [Description("設定済み対象ブランチの現在のHEADへポリシー準拠タグを作成します。 / Creates a policy-compliant tag at the configured target branch's current HEAD.")]
    public Task<BuckettieToolResult<BitbucketTagInfo>> CreateTagAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("ポリシー準拠のタグ名。 / Policy-compliant tag name.")] string tag,
        [Description("任意の注釈付きタグメッセージ。 / Optional annotated-tag message.")] string? message = null,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.CreateTagAsync(repository, new BitbucketTagCreate(tag, message), cancellationToken),
            "tag_create",
            repository,
            _language);

    /// <summary>Policy準拠のRemote Tagを削除します。</summary>
    [McpServerTool(Name = "bitbucket_tag_delete", Destructive = true, Idempotent = false,
        OpenWorld = true, UseStructuredContent = true)]
    [Description("ポリシー準拠のBitbucketタグを削除します。 / Deletes a policy-compliant Bitbucket tag.")]
    public Task<BuckettieToolResult<bool>> DeleteTagAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("削除するタグ名。 / Tag name to delete.")] string tag,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapBitbucketAsync(
            _bitbucket.DeleteTagAsync(repository, tag, cancellationToken),
            "tag_delete", repository, _language);

    /// <summary>Policy準拠のLocal Tagを明示的にpushします。</summary>
    [McpServerTool(Name = "bitbucket_tag_push", Destructive = true, Idempotent = true,
        OpenWorld = true, UseStructuredContent = true)]
    [Description("ポリシー準拠のローカルタグを設定済みBitbucketリモートへ明示的にpushします。 / Explicitly pushes a policy-compliant local tag to the configured Bitbucket remote.")]
    public Task<BuckettieToolResult<BuckettieGitData>> PushTagAsync(
        [Description("BuckettieリポジトリID。 / Buckettie repository ID.")] string repository,
        [Description("pushするタグ名。 / Tag name to push.")] string tag,
        CancellationToken cancellationToken = default) =>
        BuckettieToolResultMapper.MapGitAsync(_git.PushTagAsync(repository, tag, cancellationToken), _language);

    /// <summary>新規RepositoryをAllowlistへ登録します。対話Desktopでの人間承認が必須です。</summary>
    [McpServerTool(Name = "bitbucket_repository_register", ReadOnly = false, Destructive = true,
        Idempotent = false, OpenWorld = true, UseStructuredContent = true)]
    [Description("新しいリポジトリの許可リスト登録を提案し、サーバーのデスクトップで対話承認を要求します。 / " +
        "Proposes registering a new repository in the allowlist; requires interactive human approval " +
        "on the server's desktop session. Workspace/Slug are always derived from the local Git remote, never " +
        "from caller input, branch policy fields are server-defaulted, and the Git remote must use HTTPS.")]
    public async Task<BuckettieToolResult<BuckettieRepositoryRegistrationData>> RegisterRepositoryAsync(
        [Description("登録する新しいBuckettieリポジトリID。 / New Buckettie repository ID to register.")] string repository,
        [Description("登録する既存Gitリポジトリの絶対ローカルパス。 / Absolute local path of the existing Git repository to register.")] string localRoot,
        [Description("検証して使用するHTTPS Gitリモート名。 / HTTPS Git remote name to validate and use.")] string remote = "origin",
        [Description("開発ブランチ名。 / Development branch name.")] string developBranch = "develop",
        [Description("主要ブランチ名。 / Main branch name.")] string mainBranch = "main",
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
    [Description("登録済みリポジトリを許可リストから削除します。権限を減らす操作のため対話承認は不要です。 / " +
        "Removes a registered repository from the allowlist. Since this only revokes push/PR/tag " +
        "rights, no interactive approval is required.")]
    public async Task<BuckettieToolResult<BuckettieRepositoryUnregistrationData>> UnregisterRepositoryAsync(
        [Description("登録解除するBuckettieリポジトリID。 / Buckettie repository ID to unregister.")] string repository,
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
    [Description("登録済みリポジトリのブランチポリシー更新を提案し、対話承認を要求します。 / " +
        "Proposes updating a registered repository's branch policy (direct-push/pull/protected " +
        "branches, tag target/pattern, require-clean-working-tree); requires interactive human approval on " +
        "the server's desktop session. Workspace/Slug/LocalRoot/Remote/DevelopBranch/MainBranch cannot be " +
        "changed here — unregister and re-register instead.")]
    public async Task<BuckettieToolResult<BuckettieRepositoryUpdateData>> UpdateRepositoryAsync(
        [Description("更新するBuckettieリポジトリID。 / Buckettie repository ID to update.")] string repository,
        [Description("直接pushを許可するブランチ。 / Branches allowed to push directly.")] HashSet<string> directPushBranches,
        [Description("pullを許可するブランチ。 / Branches allowed to be pulled.")] HashSet<string> pullBranches,
        [Description("直接pushから保護するブランチ。 / Branches that are protected from direct push.")] HashSet<string> protectedBranches,
        [Description("リリースタグの対象ブランチ。 / Branch that release tags target.")] string tagTargetBranch,
        [Description("許可するリリースタグ名が一致すべき正規表現。 / Regular expression allowed release tag names must match.")] string tagPattern,
        [Description("pushにクリーンな作業ツリーを要求するか。 / Whether push requires a clean working tree.")] bool requireCleanWorkingTree = true,
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
