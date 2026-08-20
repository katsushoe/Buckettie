using FluentAssertions;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Git;
using ModelContextProtocol.Server;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class BuckettieMcpToolsTests
{
    private static readonly string[] ExpectedToolNames =
    [
        "get_version",
        "bitbucket_repository_status",
        "bitbucket_fetch",
        "bitbucket_pull",
        "bitbucket_push",
        "bitbucket_branch_list",
        "bitbucket_branch_get",
        "bitbucket_pr_list",
        "bitbucket_pr_get",
        "bitbucket_pr_diff",
        "bitbucket_pr_create",
        "bitbucket_pr_merge",
        "bitbucket_tag_list",
        "bitbucket_tag_get",
        "bitbucket_tag_create",
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
            "bitbucket_pr_create",
            "bitbucket_pr_merge",
            "bitbucket_tag_create",
            "bitbucket_repository_register",
            "bitbucket_repository_unregister",
            "bitbucket_repository_update");
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

        result.Should().Be(new BuckettieToolResult<BuckettieGitData>(
            false,
            "push",
            "example",
            null,
            new BuckettieToolError("protected_branch", "Direct push to the protected branch is not allowed.")));
    }

    [Fact]
    public async Task MapGitAsync_WhenLanguageIsJapanese_ReturnsJapaneseMessageAndStableCode()
    {
        GitGatewayResult gatewayResult = GitGatewayResult.Failure(
            "push", "example", GitGatewayError.ProtectedBranch, "main");

        BuckettieToolResult<BuckettieGitData> result = await BuckettieToolResultMapper.MapGitAsync(
            Task.FromResult(gatewayResult), "ja-JP");

        result.Error.Should().Be(new BuckettieToolError(
            "protected_branch", "保護ブランチへの直接pushは許可されていません。"));
    }

    [Theory]
    [InlineData("pr_get", "pull_request_not_found")]
    [InlineData("pr_diff", "pull_request_not_found")]
    [InlineData("pr_merge", "pull_request_not_found")]
    [InlineData("tag_get", "tag_not_found")]
    [InlineData("branch_get", "branch_not_found")]
    [InlineData("branch_list", "repository_not_found")]
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

    private sealed class UnusedGitGateway : IGitGateway
    {
        public Task<GitGatewayResult> GetStatusAsync(string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> FetchAsync(string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> PullAsync(string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<GitGatewayResult> PushAsync(string repository, CancellationToken cancellationToken = default) =>
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

        public Task<BitbucketResult<IReadOnlyList<BitbucketTagInfo>>> ListTagsAsync(
            string repository, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketTagInfo>> GetTagAsync(
            string repository, string tag, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<BitbucketResult<BitbucketTagInfo>> CreateTagAsync(
            string repository, BitbucketTagCreate input, CancellationToken cancellationToken = default) =>
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
