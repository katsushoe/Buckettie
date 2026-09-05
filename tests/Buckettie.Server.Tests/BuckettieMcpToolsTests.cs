using FluentAssertions;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Configuration;
using Buckettie.Application.Git;
using Buckettie.Application.Repositories;
using ModelContextProtocol.Server;
using System.ComponentModel;
using System.Text.Json;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class BuckettieMcpToolsTests
{
    private static readonly string[] ExpectedToolNames =
    [
        "get_version",
        "list_projects",
        "bitbucket_provider_capabilities",
        "bitbucket_repository_status",
        "bitbucket_repository_diff",
        "bitbucket_repository_commit",
        "bitbucket_history_rewrite_preview",
        "bitbucket_history_rewrite_execute",
        "bitbucket_force_push_with_lease",
        "bitbucket_fetch",
        "bitbucket_pull",
        "bitbucket_push",
        "bitbucket_branch_list",
        "bitbucket_branch_get",
        "bitbucket_branch_create",
        "bitbucket_branch_delete",
        "bitbucket_pr_list",
        "bitbucket_pr_get",
        "bitbucket_pr_diff",
        "bitbucket_pr_create",
        "bitbucket_pr_merge",
        "bitbucket_tag_list",
        "bitbucket_tag_get",
        "bitbucket_tag_create",
        "bitbucket_tag_delete",
        "bitbucket_tag_push",
        "buckettie_release_create",
        "buckettie_release_publish",
        "buckettie_release_get",
        "buckettie_release_withdraw",
        "bitbucket_repository_register",
        "bitbucket_repository_unregister",
        "bitbucket_repository_update",
    ];

    [Fact]
    public void JsonOptions_WhenSdkMakesOptionsReadOnly_HasTypeInfoResolver()
    {
        Action act = () => BuckettieMcpJson.CreateOptions().MakeReadOnly();

        act.Should().NotThrow();
    }

    [Fact]
    public void McpGuidance_WhenInspected_ExposesUsagePromptAndServerInstructions()
    {
        McpServerPromptAttribute attribute = typeof(BuckettieMcpGuidance)
            .GetMethod(nameof(BuckettieMcpGuidance.GetUsageGuide))!
            .GetCustomAttributes(typeof(McpServerPromptAttribute), inherit: false)
            .Cast<McpServerPromptAttribute>()
            .Single();

        attribute.Name.Should().Be("buckettie_usage");
        BuckettieMcpGuidance.ServerInstructions.Should().Contain("localhost gateway");
        BuckettieMcpGuidance.ServerInstructions.Should().Contain("bitbucket_*");
        BuckettieMcpGuidance.ServerInstructions.Should().Contain(
            "Before every push in every conversation, call list_projects");
        new BuckettieMcpGuidance().GetUsageGuide().Should().Be(BuckettieMcpGuidance.ServerInstructions);
    }

    [Fact]
    public void ToolMethods_WhenInspected_ExposeExactlyThePhaseOneToolSet()
    {
        McpServerToolAttribute[] attributes = typeof(BuckettieMcpTools)
            .GetMethods()
            .Select(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false)
                .Cast<McpServerToolAttribute>()
                .SingleOrDefault())
            .Where(attribute => attribute is not null)
            .Cast<McpServerToolAttribute>()
            .ToArray();

        attributes.Select(attribute => attribute.Name).Should().BeEquivalentTo(ExpectedToolNames);
        attributes.Should().OnlyContain(attribute => attribute.UseStructuredContent);
    }

    [Fact]
    public void ToolMethods_WhenMutationIsImportant_MarkItDestructive()
    {
        string[] destructive = typeof(BuckettieMcpTools)
            .GetMethods()
            .Select(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false)
                .Cast<McpServerToolAttribute>()
                .SingleOrDefault())
            .Where(attribute => attribute?.Destructive == true)
            .Select(attribute => attribute!.Name!)
            .ToArray();

        destructive.Should().BeEquivalentTo(
            "bitbucket_push",
            "bitbucket_repository_commit",
            "bitbucket_history_rewrite_execute",
            "bitbucket_force_push_with_lease",
            "bitbucket_branch_create",
            "bitbucket_branch_delete",
            "bitbucket_pr_create",
            "bitbucket_pr_merge",
            "bitbucket_tag_create",
            "bitbucket_tag_delete",
            "bitbucket_tag_push",
            "buckettie_release_create",
            "buckettie_release_publish",
            "buckettie_release_withdraw",
            "bitbucket_repository_register",
            "bitbucket_repository_unregister",
            "bitbucket_repository_update");
    }

    [Fact]
    public void ToolMethods_WhenDescriptionsArePublished_UseBilingualMetadata()
    {
        DescriptionAttribute[] descriptions = typeof(BuckettieMcpTools)
            .GetMethods()
            .Where(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), false).Length > 0)
            .SelectMany(method => method.GetCustomAttributes(typeof(DescriptionAttribute), false)
                .Cast<DescriptionAttribute>()
                .Concat(method.GetParameters().SelectMany(parameter =>
                    parameter.GetCustomAttributes(typeof(DescriptionAttribute), false).Cast<DescriptionAttribute>())))
            .ToArray();

        descriptions.Should().NotBeEmpty().And.OnlyContain(description =>
            description.Description.Contains(" / ", StringComparison.Ordinal));
    }

    [Fact]
    public void RepositoryContractMethods_WhenInspected_ExposeRequiredInputs()
    {
        string[] diffInputs = typeof(BuckettieMcpTools)
            .GetMethod(nameof(BuckettieMcpTools.RepositoryDiffAsync))!
            .GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .Select(parameter => parameter.Name!)
            .ToArray();
        string[] commitInputs = typeof(BuckettieMcpTools)
            .GetMethod(nameof(BuckettieMcpTools.RepositoryCommitAsync))!
            .GetParameters()
            .Where(parameter => parameter.ParameterType != typeof(CancellationToken))
            .Select(parameter => parameter.Name!)
            .ToArray();

        diffInputs.Should().Equal("repository");
        commitInputs.Should().Equal("repository", "message");
        var branchInputs = typeof(BuckettieMcpTools).GetMethod(nameof(BuckettieMcpTools.CreateBranchAsync))!
            .GetParameters().Where(parameter => parameter.ParameterType != typeof(CancellationToken)).ToArray();
        branchInputs.Select(parameter => parameter.Name).Should().Equal("repository", "branch", "source");
        branchInputs.Should().OnlyContain(parameter => !parameter.IsOptional);
    }

    [Fact]
    public void RegisterRepositoryAsync_WhenInspected_IsNotReadOnlyOrIdempotentAndIsOpenWorld()
    {
        McpServerToolAttribute attribute = typeof(BuckettieMcpTools)
            .GetMethod(nameof(BuckettieMcpTools.RegisterRepositoryAsync))!
            .GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false)
            .Cast<McpServerToolAttribute>()
            .Single();

        attribute.ReadOnly.Should().BeFalse();
        attribute.Idempotent.Should().BeFalse();
        attribute.OpenWorld.Should().BeTrue();
        attribute.Destructive.Should().BeTrue();
    }

    [Fact]
    public void UnregisterRepositoryAsync_WhenInspected_IsNotOpenWorld()
    {
        McpServerToolAttribute attribute = typeof(BuckettieMcpTools)
            .GetMethod(nameof(BuckettieMcpTools.UnregisterRepositoryAsync))!
            .GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false)
            .Cast<McpServerToolAttribute>()
            .Single();

        attribute.ReadOnly.Should().BeFalse();
        attribute.Idempotent.Should().BeFalse();
        attribute.OpenWorld.Should().BeFalse();
        attribute.Destructive.Should().BeTrue();
    }

    [Fact]
    public void UpdateRepositoryAsync_WhenInspected_IsNotOpenWorld()
    {
        McpServerToolAttribute attribute = typeof(BuckettieMcpTools)
            .GetMethod(nameof(BuckettieMcpTools.UpdateRepositoryAsync))!
            .GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false)
            .Cast<McpServerToolAttribute>()
            .Single();

        attribute.ReadOnly.Should().BeFalse();
        attribute.Idempotent.Should().BeFalse();
        attribute.OpenWorld.Should().BeFalse();
        attribute.Destructive.Should().BeTrue();
    }

    [Fact]
    public async Task UnregisterRepositoryAsync_WhenServiceSucceeds_ReturnsOkResult()
    {
        BuckettieMcpTools tools = new(
            new UnusedGitGateway(),
            new UnusedBitbucketRepositoryGateway(),
            new UnusedRepositoryRegistrationService(),
            new StubRepositoryUnregistrationService(RepositoryUnregistrationOutcome.Success("buckettie")),
            new UnusedRepositoryUpdateService());

        BuckettieToolResult<BuckettieRepositoryUnregistrationData> result =
            await tools.UnregisterRepositoryAsync("buckettie", TestContext.Current.CancellationToken);

        result.Ok.Should().BeTrue();
        result.Data!.RepositoryId.Should().Be("buckettie");
    }

    [Fact]
    public async Task UpdateRepositoryAsync_WhenServiceFails_ReturnsErrorResult()
    {
        BuckettieToolError error = new("repository_not_registered", "The repository is not registered.");
        BuckettieMcpTools tools = new(
            new UnusedGitGateway(),
            new UnusedBitbucketRepositoryGateway(),
            new UnusedRepositoryRegistrationService(),
            new UnusedRepositoryUnregistrationService(),
            new StubRepositoryUpdateService(RepositoryUpdateOutcome.Failure(error)));

        BuckettieToolResult<BuckettieRepositoryUpdateData> result = await tools.UpdateRepositoryAsync(
            "unknown", ["develop"], ["develop", "main"], ["main"], "main", "^v[0-9]+.*$",
            cancellationToken: TestContext.Current.CancellationToken);

        result.Ok.Should().BeFalse();
        result.Error.Should().Be(error);
    }

    [Fact]
    public void ToolMethods_WhenInspected_ReturnCommonStructuredResult()
    {
        Type[] returnTypes = typeof(BuckettieMcpTools)
            .GetMethods()
            .Where(method => method.GetCustomAttributes(typeof(McpServerToolAttribute), inherit: false).Length == 1)
            .Select(method => method.ReturnType.GenericTypeArguments.Single())
            .ToArray();

        returnTypes.Should().OnlyContain(type =>
            type.IsGenericType && type.GetGenericTypeDefinition() == typeof(BuckettieToolResult<>));
    }

    [Fact]
    public async Task MapGitAsync_WhenGatewayFails_ReturnsCommonErrorShape()
    {
        GitGatewayResult gatewayResult = GitGatewayResult.Failure(
            "push", "example", GitGatewayError.ProtectedBranch, "main");

        BuckettieToolResult<BuckettieGitData> result = await BuckettieToolResultMapper.MapGitAsync(
            Task.FromResult(gatewayResult));

        result.Ok.Should().BeFalse();
        result.Operation.Should().Be("push");
        result.Repository.Should().Be("example");
        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be("protected_branch");
        result.Error.Message.Should().Be("Direct push to the protected branch is not allowed.");
        result.Error.Summary.Should().Contain("Git push");
        result.Error.SuggestedAction.Should().NotBeNullOrWhiteSpace();
        result.Error.Category.Should().Be("protected_branch");
        result.Error.Details.Should().BeNull();
    }

    [Fact]
    public async Task MapGitAsync_WhenGitFailed_ReturnsCategoryAndDiagnosticDetails()
    {
        GitGatewayResult gatewayResult = GitGatewayResult.DiagnosticFailure(
            "fetch", "example", GitGatewayError.GitFailed, errorDetail: "fatal: broken repository");

        BuckettieToolResult<BuckettieGitData> result = await BuckettieToolResultMapper.MapGitAsync(
            Task.FromResult(gatewayResult));

        result.Error!.Category.Should().Be("git_failed");
        result.Error.Details.Should().Be("fatal: broken repository");

        using JsonDocument json = JsonDocument.Parse(
            JsonSerializer.Serialize(result, BuckettieMcpJson.CreateOptions()));
        JsonElement error = json.RootElement.GetProperty("error");
        error.GetProperty("category").GetString().Should().Be("git_failed");
        error.GetProperty("details").GetString().Should().Be("fatal: broken repository");
    }

    [Fact]
    public async Task MapGitAsync_WhenLanguageIsJapanese_ReturnsJapaneseMessageAndStableCode()
    {
        GitGatewayResult gatewayResult = GitGatewayResult.Failure(
            "push", "example", GitGatewayError.ProtectedBranch, "main");

        BuckettieToolResult<BuckettieGitData> result = await BuckettieToolResultMapper.MapGitAsync(
            Task.FromResult(gatewayResult), "ja-JP");

        result.Error!.Code.Should().Be("protected_branch");
        result.Error.Message.Should().Be("保護ブランチへの直接pushは許可されていません。");
        result.Error.SuggestedAction.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task MapGitAsync_WhenRemoteUsesSsh_ReturnsMigrationGuidance()
    {
        GitGatewayResult gatewayResult = GitGatewayResult.Failure(
            "fetch", "example", GitGatewayError.SshRemoteNotSupported);

        BuckettieToolResult<BuckettieGitData> result = await BuckettieToolResultMapper.MapGitAsync(
            Task.FromResult(gatewayResult), "ja-JP");

        result.Error!.Code.Should().Be("ssh_remote_not_supported");
        result.Error.Message.Should().Be(
            "SSH形式のGitリモートには対応していません。BitbucketのHTTPS URLへ変更してください。");
    }

    [Theory]
    [InlineData(BitbucketError.MergeabilityCalculating, "mergeability_calculating", "calculating_retryable", true, 2)]
    [InlineData(BitbucketError.MergeabilityUnknown, "mergeability_unknown", "unknown_retryable", true, 2)]
    [InlineData(BitbucketError.PullRequestMergeConflict, "pull_request_merge_conflict", "conflicting", false, null)]
    [InlineData(BitbucketError.PullRequestMergeBlocked, "pull_request_merge_blocked", "blocked", false, null)]
    public async Task MapBitbucketAsync_WhenMergeFails_ReturnsCommonMergeabilityContract(
        BitbucketError gatewayError,
        string code,
        string status,
        bool retryable,
        int? retryAfterSeconds)
    {
        BuckettieToolResult<BitbucketPullRequestInfo> result = await BuckettieToolResultMapper.MapBitbucketAsync(
            Task.FromResult(BitbucketResult<BitbucketPullRequestInfo>.Failure(gatewayError)),
            "pr_merge",
            "example");

        result.Error.Should().NotBeNull();
        result.Error!.Code.Should().Be(code);
        result.Error.Status.Should().Be(status);
        result.Error.Retryable.Should().Be(retryable);
        result.Error.RetryAfterSeconds.Should().Be(retryAfterSeconds);
    }

    [Theory]
    [InlineData("pr_get", "pull_request_not_found")]
    [InlineData("pr_diff", "pull_request_not_found")]
    [InlineData("pr_merge", "pull_request_not_found")]
    [InlineData("tag_get", "tag_not_found")]
    [InlineData("tag_delete", "tag_not_found")]
    [InlineData("branch_get", "branch_not_found")]
    [InlineData("branch_delete", "branch_not_found")]
    [InlineData("branch_list", "repository_not_found")]
    [InlineData("branch_create", "branch_source_not_found")]
    public void BitbucketCode_WhenApiReturnsNotFound_UsesOperationContext(string operation, string expected)
    {
        BuckettieToolResultMapper.BitbucketCode(BitbucketError.NotFound, operation).Should().Be(expected);
    }

    [Fact]
    public async Task GetVersionAsync_WhenCalled_ReturnsRunningAssemblyVersion()
    {
        BuckettieMcpTools tools = new(
            new UnusedGitGateway(),
            new UnusedBitbucketRepositoryGateway(),
            new UnusedRepositoryRegistrationService(),
            new UnusedRepositoryUnregistrationService(),
            new UnusedRepositoryUpdateService());
        string expectedVersion = typeof(BuckettieMcpTools).Assembly.GetName().Version!.ToString();

        BuckettieToolResult<BuckettieVersionData> result = await tools.GetVersionAsync();

        result.Should().Be(new BuckettieToolResult<BuckettieVersionData>(
            true, "get_version", string.Empty, new BuckettieVersionData(expectedVersion), null));
    }

    [Fact]
    public async Task ListProjectsAsync_WhenCalled_ReturnsSortedRegisteredIds()
    {
        BuckettieOptions options = new()
        {
            AtlassianEmail = "developer@example.com",
            BitbucketUsername = "developer",
            Repositories = new Dictionary<string, RepositoryOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["zeta"] = CreateRepository("zeta"),
                ["alpha"] = CreateRepository("alpha"),
            },
        };
        BuckettieMcpTools tools = new(
            new UnusedGitGateway(),
            new UnusedBitbucketRepositoryGateway(),
            new UnusedRepositoryRegistrationService(),
            new UnusedRepositoryUnregistrationService(),
            new UnusedRepositoryUpdateService(),
            options,
            new RepositoryAllowlist(options));

        BuckettieToolResult<BuckettieProjectListData> result = await tools.ListProjectsAsync();

        result.Ok.Should().BeTrue();
        result.Data!.Projects.Should().Equal("alpha", "zeta");
    }

    [Fact]
    public async Task MapGitAsync_WhenPushRepositoryIsUnknown_ReturnsProjectCandidates()
    {
        GitGatewayResult gatewayResult = GitGatewayResult.Failure(
            "push", "unknown", GitGatewayError.RepositoryNotAllowed);

        BuckettieToolResult<BuckettieGitData> result = await BuckettieToolResultMapper.MapGitAsync(
            Task.FromResult(gatewayResult), projectCandidates: ["alpha", "zeta"]);

        result.Error!.ProjectCandidates.Should().Equal("alpha", "zeta");
    }

    private static RepositoryOptions CreateRepository(string slug) => new()
    {
        Workspace = "example-workspace",
        Slug = slug,
        LocalRoot = "repository-root",
        Remote = "origin",
        DevelopBranch = "develop",
        MainBranch = "main",
        DirectPushBranches = new HashSet<string> { "develop" },
        PullBranches = new HashSet<string> { "develop", "main" },
        ProtectedBranches = new HashSet<string> { "main" },
        TagTargetBranch = "main",
        TagPattern = "^v[0-9]+\\.[0-9]+\\.[0-9]+.*$",
    };

    [Fact]
    public async Task GetProviderCapabilitiesAsync_WhenCalled_MatchesImplementedContractTools()
    {
        BuckettieMcpTools tools = new(
            new UnusedGitGateway(),
            new UnusedBitbucketRepositoryGateway(),
            new UnusedRepositoryRegistrationService(),
            new UnusedRepositoryUnregistrationService(),
            new UnusedRepositoryUpdateService());

        BuckettieToolResult<BitbucketProviderCapabilities> result =
            await tools.GetProviderCapabilitiesAsync();

        result.Ok.Should().BeTrue();
        result.Data!.Provider.Should().Be("bitbucket");
        result.Data.Operations.Should().Contain(
        [
            new KeyValuePair<string, bool>("branch_create", true),
            new KeyValuePair<string, bool>("branch_delete", true),
            new KeyValuePair<string, bool>("tag_delete", true),
            new KeyValuePair<string, bool>("tag_push", true),
            new KeyValuePair<string, bool>("explicit_push", true),
            new KeyValuePair<string, bool>("repository_diff", true),
            new KeyValuePair<string, bool>("repository_commit", true),
        ]);
    }

    private sealed class UnusedGitGateway : IGitGateway
    {
        public Task<GitGatewayResult> GetStatusAsync(string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> GetDiffAsync(string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> CommitAsync(
            string repository, string message, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> FetchAsync(string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> PullAsync(string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> PushAsync(string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> PushTagAsync(
            string repository, string tag, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> PreviewHistoryRewriteAsync(
            string repository, GitHistoryRewriteRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> RewriteHistoryAsync(
            string repository, GitHistoryRewriteRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> ForcePushWithLeaseAsync(
            string repository, GitForceWithLeaseRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedBitbucketRepositoryGateway : IBitbucketRepositoryGateway
    {
        public Task<BitbucketResult<BitbucketRepositoryInfo>> GetRepositoryAsync(
            string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<IReadOnlyList<BitbucketBranchInfo>>> ListBranchesAsync(
            string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketBranchInfo>> GetBranchAsync(
            string repository, string branch, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketBranchInfo>> CreateBranchAsync(
            string repository, string branch, string source, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<bool>> DeleteBranchAsync(
            string repository, string branch, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<IReadOnlyList<BitbucketTagInfo>>> ListTagsAsync(
            string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketTagInfo>> GetTagAsync(
            string repository, string tag, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketTagInfo>> CreateTagAsync(
            string repository, BitbucketTagCreate input, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<bool>> DeleteTagAsync(
            string repository, string tag, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketReleaseInfo>> CreateReleaseAsync(
            string repository, string version, string? notes, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketReleaseInfo>> PublishReleaseAsync(
            string repository, string version, string? artifactPath, string? notes,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketReleaseInfo>> GetReleaseAsync(
            string repository, string version, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<bool>> WithdrawReleaseAsync(
            string repository, string version, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>> ListPullRequestsAsync(
            string repository, BitbucketPullRequestState? state, string? source, string? destination,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketPullRequestInfo>> GetPullRequestAsync(
            string repository, int pullRequestId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<string>> GetPullRequestDiffAsync(
            string repository, int pullRequestId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketPullRequestInfo>> CreatePullRequestAsync(
            string repository, BitbucketPullRequestCreate input, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketPullRequestInfo>> MergePullRequestAsync(
            string repository, int pullRequestId, BitbucketPullRequestMerge input,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedRepositoryRegistrationService : IRepositoryRegistrationService
    {
        public Task<RepositoryRegistrationOutcome> RegisterAsync(
            string repositoryId, string localRoot, string remote, string developBranch, string mainBranch,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedRepositoryUnregistrationService : IRepositoryUnregistrationService
    {
        public Task<RepositoryUnregistrationOutcome> UnregisterAsync(
            string repositoryId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class UnusedRepositoryUpdateService : IRepositoryUpdateService
    {
        public Task<RepositoryUpdateOutcome> UpdateAsync(
            string repositoryId, RepositoryUpdateRequest request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class StubRepositoryUnregistrationService(RepositoryUnregistrationOutcome outcome)
        : IRepositoryUnregistrationService
    {
        public Task<RepositoryUnregistrationOutcome> UnregisterAsync(
            string repositoryId, CancellationToken cancellationToken) => Task.FromResult(outcome);
    }

    private sealed class StubRepositoryUpdateService(RepositoryUpdateOutcome outcome) : IRepositoryUpdateService
    {
        public Task<RepositoryUpdateOutcome> UpdateAsync(
            string repositoryId, RepositoryUpdateRequest request, CancellationToken cancellationToken) =>
            Task.FromResult(outcome);
    }
}
