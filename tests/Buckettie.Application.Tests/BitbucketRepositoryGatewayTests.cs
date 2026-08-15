using Buckettie.Application.Bitbucket;
using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Application.Tests;

public sealed class BitbucketRepositoryGatewayTests
{
    private readonly IBitbucketApiClient _client = Substitute.For<IBitbucketApiClient>();

    [Fact]
    public async Task GetRepositoryAsync_WhenRepositoryIsAllowed_UsesConfiguredCoordinates()
    {
        BitbucketRepositoryInfo expected = new("{uuid}", "workspace/repository", "Repository", true, "main");
        _client.GetRepositoryAsync(
            "allowed",
            "workspace",
            "repository",
            Arg.Any<CancellationToken>()).Returns(BitbucketResult<BitbucketRepositoryInfo>.Success(expected));
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<BitbucketRepositoryInfo> result = await gateway.GetRepositoryAsync(
            "allowed",
            TestContext.Current.CancellationToken);

        result.Value.Should().Be(expected);
    }

    [Fact]
    public async Task ListBranchesAsync_WhenRepositoryIsUnknown_DoesNotCallApi()
    {
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<IReadOnlyList<BitbucketBranchInfo>> result = await gateway.ListBranchesAsync(
            "unknown",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.RepositoryNotAllowed);
        await _client.DidNotReceiveWithAnyArgs().ListBranchesAsync(
            default!,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetBranchAsync_WhenBranchContainsControlCharacter_DoesNotCallApi()
    {
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<BitbucketBranchInfo> result = await gateway.GetBranchAsync(
            "allowed",
            "main\nother",
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.InvalidBranch);
        await _client.DidNotReceiveWithAnyArgs().GetBranchAsync(
            default!,
            default!,
            default!,
            default!,
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateTagAsync_WhenInputIsValid_TargetsConfiguredBranchHead()
    {
        BitbucketTagCreate input = new("v1.2.3", "Release");
        BitbucketTagInfo expected = new("v1.2.3", "abcdef", "Release", null, null);
        _client.GetBranchAsync(
            "allowed",
            "workspace",
            "repository",
            "main",
            Arg.Any<CancellationToken>()).Returns(BitbucketResult<BitbucketBranchInfo>.Success(
                new BitbucketBranchInfo("main", "abcdef")));
        _client.CreateTagAsync(
            "allowed",
            "workspace",
            "repository",
            "abcdef",
            input,
            Arg.Any<CancellationToken>()).Returns(BitbucketResult<BitbucketTagInfo>.Success(expected));
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<BitbucketTagInfo> result = await gateway.CreateTagAsync(
            "allowed",
            input,
            TestContext.Current.CancellationToken);

        result.Value.Should().Be(expected);
    }

    [Fact]
    public async Task CreateTagAsync_WhenNameViolatesPattern_DoesNotCallApi()
    {
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<BitbucketTagInfo> result = await gateway.CreateTagAsync(
            "allowed",
            new BitbucketTagCreate("release-1", null),
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.InvalidTag);
        await _client.DidNotReceiveWithAnyArgs().CreateTagAsync(
            default!, default!, default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreatePullRequestAsync_WhenInputIsValid_UsesConfiguredDevelopToMainRoute()
    {
        BitbucketPullRequestCreate input = new("Release", "Description", false);
        BitbucketPullRequestInfo expected = CreatePullRequest("OPEN", "develop", "main");
        _client.CreatePullRequestAsync(
            "allowed",
            "workspace",
            "repository",
            "develop",
            "main",
            input,
            Arg.Any<CancellationToken>()).Returns(BitbucketResult<BitbucketPullRequestInfo>.Success(expected));
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<BitbucketPullRequestInfo> result = await gateway.CreatePullRequestAsync(
            "allowed",
            input,
            TestContext.Current.CancellationToken);

        result.Value.Should().Be(expected);
    }

    [Fact]
    public async Task MergePullRequestAsync_WhenRouteIsNotAllowed_DoesNotMerge()
    {
        _client.GetPullRequestAsync(
            "allowed",
            "workspace",
            "repository",
            7,
            Arg.Any<CancellationToken>()).Returns(BitbucketResult<BitbucketPullRequestInfo>.Success(
                CreatePullRequest("OPEN", "main", "develop")));
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<BitbucketPullRequestInfo> result = await gateway.MergePullRequestAsync(
            "allowed",
            7,
            new BitbucketPullRequestMerge(BitbucketMergeStrategy.RepositoryDefault, null),
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.PullRequestRouteNotAllowed);
        await _client.DidNotReceiveWithAnyArgs().MergePullRequestAsync(
            default!, default!, default!, default, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task MergePullRequestAsync_WhenPullRequestIsNotOpen_DoesNotMerge()
    {
        _client.GetPullRequestAsync(
            "allowed",
            "workspace",
            "repository",
            7,
            Arg.Any<CancellationToken>()).Returns(BitbucketResult<BitbucketPullRequestInfo>.Success(
                CreatePullRequest("MERGED", "develop", "main")));
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<BitbucketPullRequestInfo> result = await gateway.MergePullRequestAsync(
            "allowed",
            7,
            new BitbucketPullRequestMerge(BitbucketMergeStrategy.RepositoryDefault, null),
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.PullRequestNotOpen);
    }

    private static BitbucketPullRequestInfo CreatePullRequest(
        string state,
        string source,
        string destination) => new(
        7,
        "Release",
        "Description",
        state,
        source,
        destination,
        false,
        "https://bitbucket.org/workspace/repository/pull-requests/7",
        DateTimeOffset.Parse("2026-08-16T00:00:00Z"),
        DateTimeOffset.Parse("2026-08-16T01:00:00Z"),
        null);

    private BitbucketRepositoryGateway CreateGateway()
    {
        BuckettieOptions options = new()
        {
            AtlassianEmail = "developer@example.com",
            BitbucketUsername = "developer",
            Repositories = new Dictionary<string, RepositoryOptions>
            {
                ["allowed"] = new()
                {
                    Workspace = "workspace",
                    Slug = "repository",
                    LocalRoot = "repository-root",
                    Remote = "origin",
                    DevelopBranch = "develop",
                    MainBranch = "main",
                    DirectPushBranches = ["develop"],
                    PullBranches = ["develop", "main"],
                    ProtectedBranches = ["main"],
                    TagTargetBranch = "main",
                    TagPattern = "^v[0-9]+\\.[0-9]+\\.[0-9]+.*$",
                },
            },
        };
        return new BitbucketRepositoryGateway(new RepositoryAllowlist(options), _client);
    }
}
