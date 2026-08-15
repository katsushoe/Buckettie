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
