using Buckettie.Application.Git;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Application.Tests;

public sealed class GitRemoteResolverTests
{
    private const string Root = "C:\\Repositories\\CupperPro";
    private readonly IGitCommandClient _git = Substitute.For<IGitCommandClient>();

    [Fact]
    public async Task ResolveAsync_WhenOnlySshAndHttpsMatch_SelectsHttpsRemote()
    {
        ConfigureRemotes(
            "remote.origin.url git@bitbucket.org:stk2k/cupperpro.git",
            "remote.buckettie.url https://bitbucket.org/stk2k/cupperpro.git");

        GitRemoteResolution result = await Resolve(null);

        result.Status.Should().Be(GitRemoteResolutionStatus.Resolved);
        result.Name.Should().Be("buckettie");
    }

    [Fact]
    public async Task ResolveAsync_WhenMultipleHttpsMatch_PrefersNamingRule()
    {
        ConfigureRemotes(
            "remote.origin.url https://bitbucket.org/stk2k/cupperpro.git",
            "remote.bitbucket-origin-https.url https://bitbucket.org/stk2k/cupperpro/");

        GitRemoteResolution result = await Resolve(null);

        result.Name.Should().Be("bitbucket-origin-https");
    }

    [Fact]
    public async Task ResolveAsync_WhenMultipleMatchWithoutNamingRule_ReturnsAmbiguous()
    {
        ConfigureRemotes(
            "remote.origin.url https://bitbucket.org/stk2k/cupperpro.git",
            "remote.buckettie.url https://bitbucket.org/stk2k/cupperpro");

        GitRemoteResolution result = await Resolve(null);

        result.Status.Should().Be(GitRemoteResolutionStatus.Ambiguous);
    }

    [Theory]
    [InlineData("remote.origin.url git@bitbucket.org:stk2k/cupperpro.git")]
    [InlineData("remote.origin.url https://bitbucket.org/stk2k/other.git")]
    [InlineData("remote.origin.url https://bitbucket.org/stk2k/CupperPro.git")]
    [InlineData("remote.origin.url https://user:secret@bitbucket.org/stk2k/cupperpro.git")]
    [InlineData("remote.origin.url https://bitbucket.org/stk2k/cupperpro.git?x=1")]
    public async Task ResolveAsync_WhenNoSupportedRemoteMatches_ReturnsNotFound(string line)
    {
        ConfigureRemotes(line);

        GitRemoteResolution result = await Resolve(null);

        result.Status.Should().Be(GitRemoteResolutionStatus.NotFound);
    }

    [Fact]
    public async Task ResolveAsync_WhenNoRemoteIsConfigured_ReturnsNotFoundWithoutOriginFallback()
    {
        _git.ListRemoteUrlsAsync(Root, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Failed(GitCommandFailure.ReferenceNotFound));

        GitRemoteResolution result = await Resolve(null);

        result.Status.Should().Be(GitRemoteResolutionStatus.NotFound);
        await _git.DidNotReceive().GetRemoteUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenNamedRemoteMatches_UsesNamedRemote()
    {
        _git.GetRemoteUrlAsync(Root, "buckettie", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("https://bitbucket.org/stk2k/cupperpro.git\n"));

        GitRemoteResolution result = await Resolve("buckettie");

        result.Name.Should().Be("buckettie");
        await _git.DidNotReceive().ListRemoteUrlsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenNamedRemoteUrlDiffers_ReturnsMismatch()
    {
        _git.GetRemoteUrlAsync(Root, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("https://bitbucket.org/stk2k/other.git"));

        GitRemoteResolution result = await Resolve("origin");

        result.Status.Should().Be(GitRemoteResolutionStatus.Mismatch);
    }

    [Fact]
    public async Task ResolveAsync_WhenNamedRemoteIsMissing_ReturnsNotFound()
    {
        _git.GetRemoteUrlAsync(Root, "missing", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Failed(GitCommandFailure.Failed, "error: No such remote 'missing'"));

        GitRemoteResolution result = await Resolve("missing");

        result.Status.Should().Be(GitRemoteResolutionStatus.NotFound);
    }

    [Fact]
    public async Task ResolveAsync_WhenNamedRemoteIsInvalid_ReturnsNotFoundWithoutRunningGit()
    {
        GitRemoteResolution result = await Resolve("--upload-pack=x");

        result.Status.Should().Be(GitRemoteResolutionStatus.NotFound);
        await _git.DidNotReceive().GetRemoteUrlAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResolveAsync_WhenNamedRemoteIsSsh_ReturnsSshNotSupported()
    {
        _git.GetRemoteUrlAsync(Root, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("git@bitbucket.org:stk2k/cupperpro.git"));

        GitRemoteResolution result = await Resolve("origin");

        result.Status.Should().Be(GitRemoteResolutionStatus.SshRemoteNotSupported);
    }

    [Fact]
    public async Task ResolveAsync_WhenListingFails_ReturnsCommandFailure()
    {
        _git.ListRemoteUrlsAsync(Root, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Failed(GitCommandFailure.TimedOut));

        GitRemoteResolution result = await Resolve(null);

        result.Status.Should().Be(GitRemoteResolutionStatus.CommandFailed);
        result.FailedCommand!.Failure.Should().Be(GitCommandFailure.TimedOut);
    }

    [Fact]
    public async Task DiscoverAsync_WhenRemotesPointToDifferentRepositories_ReturnsAmbiguous()
    {
        ConfigureRemotes(
            "remote.origin.url https://bitbucket.org/stk2k/cupperpro.git",
            "remote.bitbucket-origin-https.url https://bitbucket.org/stk2k/other.git");

        GitRemoteResolution result = await new GitRemoteResolver(_git)
            .DiscoverAsync(Root, TestContext.Current.CancellationToken);

        result.Status.Should().Be(GitRemoteResolutionStatus.Ambiguous);
    }

    [Fact]
    public async Task DiscoverAsync_WhenSingleHttpsRemoteExists_ReturnsItsUrl()
    {
        ConfigureRemotes(
            "remote.origin.url git@bitbucket.org:stk2k/cupperpro.git",
            "remote.buckettie.url https://bitbucket.org/stk2k/cupperpro.git");

        GitRemoteResolution result = await new GitRemoteResolver(_git)
            .DiscoverAsync(Root, TestContext.Current.CancellationToken);

        result.Url.Should().Be("https://bitbucket.org/stk2k/cupperpro.git");
    }

    [Theory]
    [InlineData("bitbucket-origin-https", true)]
    [InlineData("github-origin-ssh", true)]
    [InlineData("origin", false)]
    [InlineData("bitbucket_origin_https", false)]
    public void MatchesNamingRule_FollowsContractPattern(string name, bool expected) =>
        GitRemoteResolver.MatchesNamingRule(name).Should().Be(expected);

    private void ConfigureRemotes(params string[] lines) =>
        _git.ListRemoteUrlsAsync(Root, Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success(string.Join('\n', lines) + "\n"));

    private Task<GitRemoteResolution> Resolve(string? requested) =>
        new GitRemoteResolver(_git).ResolveAsync(
            Root, "stk2k", "cupperpro", requested, TestContext.Current.CancellationToken);
}
