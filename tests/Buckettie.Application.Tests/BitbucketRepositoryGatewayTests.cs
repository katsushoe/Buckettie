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

    [Theory]
    [InlineData("main", "develop")]
    [InlineData("release/stable", "feature/test")]
    public async Task CreateBranchAsync_WhenSourceIsExplicit_UsesOnlyThatBranch(string source, string destination)
    {
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        _client.GetBranchAsync(
            "allowed", "workspace", "repository", source, Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Success(new(source, hash)));
        _client.CreateBranchAsync(
            "allowed", "workspace", "repository", new BitbucketBranchCreate(destination, hash),
            Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Success(new(destination, hash)));
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<BitbucketBranchInfo> result = await gateway.CreateBranchAsync(
            "allowed", destination, source, TestContext.Current.CancellationToken);

        result.Value.Should().Be(new BitbucketBranchInfo(destination, hash, source, "branch", hash));
        await _client.DidNotReceive().GetBranchAsync(
            "allowed", "workspace", "repository", "develop", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBranchAsync_WhenFullCommitIsExplicit_ResolvesCommitWithoutBranchLookup()
    {
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        _client.GetCommitAsync("allowed", "workspace", "repository", hash, Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<string>.Success(hash));
        _client.CreateBranchAsync("allowed", "workspace", "repository", new("feature/test", hash), Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Success(new("feature/test", hash)));

        BitbucketResult<BitbucketBranchInfo> result = await CreateGateway().CreateBranchAsync(
            "allowed", "feature/test", hash, TestContext.Current.CancellationToken);

        result.Value.Should().Be(new BitbucketBranchInfo("feature/test", hash, hash, "commit", hash));
        await _client.DidNotReceiveWithAnyArgs().GetBranchAsync(default!, default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("main~1")]
    [InlineData("main^{commit}")]
    [InlineData("-main")]
    [InlineData("main\n")]
    [InlineData("refs//main")]
    [InlineData("HEAD")]
    [InlineData("main.lock")]
    public async Task CreateBranchAsync_WhenSourceIsInvalid_DoesNotCallApi(string? source)
    {
        BitbucketResult<BitbucketBranchInfo> result = await CreateGateway().CreateBranchAsync(
            "allowed", "feature/test", source!, TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.InvalidBranchSource);
        _client.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData(BitbucketError.NotFound, BitbucketError.SourceBranchNotFound)]
    [InlineData(BitbucketError.PermissionDenied, BitbucketError.PermissionDenied)]
    [InlineData(BitbucketError.AuthenticationFailed, BitbucketError.AuthenticationFailed)]
    [InlineData(BitbucketError.Timeout, BitbucketError.Timeout)]
    public async Task CreateBranchAsync_WhenSourceLookupFails_DoesNotCreate(BitbucketError failure, BitbucketError expected)
    {
        _client.GetBranchAsync("allowed", "workspace", "repository", "main", Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Failure(failure));

        BitbucketResult<BitbucketBranchInfo> result = await CreateGateway().CreateBranchAsync(
            "allowed", "develop", "main", TestContext.Current.CancellationToken);

        result.Error.Should().Be(expected);
        await _client.DidNotReceiveWithAnyArgs().CreateBranchAsync(default!, default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateBranchAsync_WhenCommitIsMissing_ReturnsSourceNotFound()
    {
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        _client.GetCommitAsync("allowed", "workspace", "repository", hash, Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<string>.Failure(BitbucketError.NotFound));

        BitbucketResult<BitbucketBranchInfo> result = await CreateGateway().CreateBranchAsync(
            "allowed", "develop", hash, TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.SourceCommitNotFound);
        await _client.DidNotReceiveWithAnyArgs().CreateBranchAsync(default!, default!, default!, default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task CreateBranchAsync_WhenRepositoryIsUnknown_DoesNotCallApi()
    {
        BitbucketResult<BitbucketBranchInfo> result = await CreateGateway().CreateBranchAsync(
            "unknown", "develop", "main", TestContext.Current.CancellationToken);
        result.Error.Should().Be(BitbucketError.RepositoryNotAllowed);
        _client.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData(BitbucketError.BranchAlreadyExists)]
    [InlineData(BitbucketError.PermissionDenied)]
    public async Task CreateBranchAsync_WhenPostFails_PreservesErrorWithoutRetry(BitbucketError failure)
    {
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        _client.GetBranchAsync("allowed", "workspace", "repository", "main", Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Success(new("main", hash)));
        _client.CreateBranchAsync("allowed", "workspace", "repository", new("develop", hash), Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Failure(failure));

        BitbucketResult<BitbucketBranchInfo> result = await CreateGateway().CreateBranchAsync(
            "allowed", "develop", "main", TestContext.Current.CancellationToken);

        result.Error.Should().Be(failure);
        await _client.Received(1).CreateBranchAsync("allowed", "workspace", "repository", new("develop", hash), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBranchAsync_WhenProviderReturnsDifferentTarget_RejectsResponseWithoutRetry()
    {
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        _client.GetBranchAsync("allowed", "workspace", "repository", "main", Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Success(new("main", hash)));
        _client.CreateBranchAsync("allowed", "workspace", "repository", new("develop", hash), Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Success(new("develop", new string('f', 40))));

        BitbucketResult<BitbucketBranchInfo> result = await CreateGateway().CreateBranchAsync(
            "allowed", "develop", "main", TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.InvalidResponse);
        await _client.Received(1).CreateBranchAsync("allowed", "workspace", "repository", new("develop", hash), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("develop")]
    [InlineData("main")]
    public async Task DeleteBranchAsync_WhenBranchIsProtected_DoesNotCallApi(string branch)
    {
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<bool> result = await gateway.DeleteBranchAsync(
            "allowed", branch, TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.BranchProtected);
        await _client.DidNotReceiveWithAnyArgs().DeleteBranchAsync(
            default!, default!, default!, default!, TestContext.Current.CancellationToken);
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
    public async Task ListPullRequestsAsync_WhenBranchesAreSpecified_FiltersExactRoute()
    {
        IReadOnlyList<BitbucketPullRequestInfo> pullRequests =
        [
            CreatePullRequest("OPEN", "develop", "main"),
            CreatePullRequest("OPEN", "release", "main"),
            CreatePullRequest("OPEN", "develop", "preview"),
        ];
        _client.ListPullRequestsAsync(
            "allowed",
            "workspace",
            "repository",
            BitbucketPullRequestState.Open,
            Arg.Any<CancellationToken>()).Returns(
                BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>>.Success(pullRequests));
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>> result = await gateway.ListPullRequestsAsync(
            "allowed",
            BitbucketPullRequestState.Open,
            "develop",
            "main",
            TestContext.Current.CancellationToken);

        result.Value.Should().ContainSingle().Which.Should().Match<BitbucketPullRequestInfo>(pullRequest =>
            pullRequest.SourceBranch == "develop" && pullRequest.DestinationBranch == "main");
    }

    [Fact]
    public async Task ListPullRequestsAsync_WhenFilterContainsControlCharacter_DoesNotCallApi()
    {
        BitbucketRepositoryGateway gateway = CreateGateway();

        BitbucketResult<IReadOnlyList<BitbucketPullRequestInfo>> result = await gateway.ListPullRequestsAsync(
            "allowed",
            null,
            "develop\nmain",
            null,
            TestContext.Current.CancellationToken);

        result.Error.Should().Be(BitbucketError.InvalidBranch);
        await _client.DidNotReceiveWithAnyArgs().ListPullRequestsAsync(
            default!, default!, default!, default, TestContext.Current.CancellationToken);
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
