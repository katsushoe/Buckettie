using System.Text.Json;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Git;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class BranchSourceContractTests
{
    [Theory]
    [InlineData(BitbucketError.InvalidBranchSource, "branch_source_invalid")]
    [InlineData(BitbucketError.SourceBranchNotFound, "branch_not_found")]
    [InlineData(BitbucketError.SourceCommitNotFound, "branch_source_not_found")]
    public async Task CreateBranch_WhenSourceFails_ReturnsTypedError(BitbucketError failure, string code)
    {
        IBitbucketRepositoryGateway gateway = Substitute.For<IBitbucketRepositoryGateway>();
        gateway.CreateBranchAsync("example", "develop", "main", Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Failure(failure));
        BuckettieMcpTools tools = CreateTools(gateway);

        BuckettieToolResult<BitbucketBranchInfo> result = await tools.CreateBranchAsync(
            "example", "develop", "main", TestContext.Current.CancellationToken);

        result.Ok.Should().BeFalse();
        result.Error!.Code.Should().Be(code);
        result.Data.Should().BeNull();
        await gateway.Received(1).CreateBranchAsync("example", "develop", "main", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateBranch_WhenSuccessful_AuditsInputAndResolvedHash()
    {
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        IBitbucketRepositoryGateway gateway = Substitute.For<IBitbucketRepositoryGateway>();
        IBuckettieAuditLogger audit = Substitute.For<IBuckettieAuditLogger>();
        gateway.CreateBranchAsync("example", "develop", "main", Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Success(new("develop", hash, "main", "branch", hash)));

        await new AuditedBitbucketRepositoryGateway(gateway, audit).CreateBranchAsync(
            "example", "develop", "main", TestContext.Current.CancellationToken);

        audit.Received(1).Write(Arg.Is<BuckettieAuditEvent>(entry => entry.IsSuccess
            && entry.Source == "main" && entry.SourceKind == "branch" && entry.SourceHash == hash));
    }

    [Fact]
    public async Task Status_WhenComparisonUnavailable_SerializesNullInsteadOfZero()
    {
        IGitGateway git = Substitute.For<IGitGateway>();
        GitRepositoryStatus status = new("example", "main", "abc", null, "abc", null, null, false,
            "refs/remotes/origin/develop", "remote_tracking_ref_missing_or_not_fetched", ["refs/remotes/origin/develop"]);
        git.GetStatusAsync("example", Arg.Any<CancellationToken>())
            .Returns(GitGatewayResult.Success("status", "example", "main", status));
        BuckettieToolResult<BuckettieGitData> result = await BuckettieToolResultMapper.MapGitAsync(
            git.GetStatusAsync("example", TestContext.Current.CancellationToken));

        using JsonDocument json = JsonDocument.Parse(JsonSerializer.Serialize(result, BuckettieMcpJson.CreateOptions()));
        JsonElement value = json.RootElement.GetProperty("data").GetProperty("status");
        value.GetProperty("ahead").ValueKind.Should().Be(JsonValueKind.Null);
        value.GetProperty("remote_develop_head").ValueKind.Should().Be(JsonValueKind.Null);
        value.GetProperty("local_head").GetString().Should().Be("abc");
        value.GetProperty("comparison_unavailable_reason").GetString().Should().Be("remote_tracking_ref_missing_or_not_fetched");
    }

    private static BuckettieMcpTools CreateTools(IBitbucketRepositoryGateway gateway) => new(
        Substitute.For<IGitGateway>(), gateway, Substitute.For<IRepositoryRegistrationService>(),
        Substitute.For<IRepositoryUnregistrationService>(), Substitute.For<IRepositoryUpdateService>());
}
