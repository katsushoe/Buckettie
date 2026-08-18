using Buckettie.Application.Configuration;
using Buckettie.Application.Git;
using Buckettie.Application.Interactive;
using Buckettie.Application.Repositories;
using Buckettie.Infrastructure.Configuration;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class RepositoryRegistrationServiceTests : IDisposable
{
    private const string LocalRoot = "C:\\Repositories\\NewRepo";
    private readonly IRepositoryEnvironment _environment = Substitute.For<IRepositoryEnvironment>();
    private readonly IGitCommandClient _git = Substitute.For<IGitCommandClient>();
    private readonly IInteractiveApprovalPrompt _approvalPrompt = Substitute.For<IInteractiveApprovalPrompt>();
    private readonly string _temporaryDirectory;
    private readonly string _configurationPath;

    public RepositoryRegistrationServiceTests()
    {
        _environment.GetFullPath(Arg.Any<string>()).Returns(callInfo => (string)callInfo[0]);
        _environment.DirectoryExists(LocalRoot).Returns(true);
        _environment.ContainsReparsePoint(LocalRoot).Returns(false);
        _environment.GitMetadataExists(LocalRoot).Returns(true);
        _git.GetRemoteUrlAsync(LocalRoot, "origin", Arg.Any<CancellationToken>())
            .Returns(GitCommandResult.Success("https://bitbucket.org/example-workspace/new-repo.git\n"));

        _temporaryDirectory = Path.Combine(Path.GetTempPath(), $"buckettie-registration-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_temporaryDirectory);
        _configurationPath = Path.Combine(_temporaryDirectory, "buckettie.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    [Fact]
    public async Task RegisterAsync_WhenValidationFails_NeverRequestsApproval()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryRegistrationService service = CreateService(allowlist);
        _environment.DirectoryExists(LocalRoot).Returns(false);

        RepositoryRegistrationOutcome outcome = await service.RegisterAsync(
            "new-repo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("local_repository_invalid");
        await _approvalPrompt.DidNotReceive().RequestApprovalAsync(
            Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
        allowlist.TryGet("new-repo", out _).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_WhenApprovalIsDenied_DoesNotMutateAllowlistOrWriteFile()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryRegistrationService service = CreateService(allowlist);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Denied());

        RepositoryRegistrationOutcome outcome = await service.RegisterAsync(
            "new-repo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("approval_denied");
        allowlist.TryGet("new-repo", out _).Should().BeFalse();
        File.Exists(_configurationPath).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_WhenApprovalTimesOut_ReturnsTimedOutAndDoesNotMutate()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryRegistrationService service = CreateService(allowlist);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Failure(ApprovalOutcome.TimedOut));

        RepositoryRegistrationOutcome outcome = await service.RegisterAsync(
            "new-repo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("approval_timed_out");
        allowlist.TryGet("new-repo", out _).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_WhenWriteFails_DoesNotMutateAllowlist()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        string missingDirectoryPath = Path.Combine(
            _temporaryDirectory, "missing-" + Guid.NewGuid().ToString("N"), "buckettie.json");
        RepositoryRegistrationService service = CreateService(allowlist, missingDirectoryPath);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Approved());

        RepositoryRegistrationOutcome outcome = await service.RegisterAsync(
            "new-repo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeFalse();
        outcome.Error!.Code.Should().Be("registration_write_failed");
        allowlist.TryGet("new-repo", out _).Should().BeFalse();
    }

    [Fact]
    public async Task RegisterAsync_WhenApproved_WritesFileAndUpdatesAllowlist()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryRegistrationService service = CreateService(allowlist);
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(ApprovalPromptOutcome.Approved());

        RepositoryRegistrationOutcome outcome = await service.RegisterAsync(
            "new-repo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        outcome.IsSuccess.Should().BeTrue();
        outcome.Workspace.Should().Be("example-workspace");
        outcome.Slug.Should().Be("new-repo");
        allowlist.TryGet("new-repo", out RepositoryOptions? registered).Should().BeTrue();
        registered!.ProtectedBranches.Should().BeEquivalentTo(["main"]);
        registered.DirectPushBranches.Should().BeEquivalentTo(["develop"]);
        File.Exists(_configurationPath).Should().BeTrue();

        JsonBuckettieOptionsLoader loader = new();
        await using FileStream stream = File.OpenRead(_configurationPath);
        ConfigurationLoadResult reloaded = await loader.LoadAsync(stream, TestContext.Current.CancellationToken);
        reloaded.IsValid.Should().BeTrue();
        reloaded.Options!.Repositories.Should().ContainKey("new-repo");
    }

    [Fact]
    public async Task RegisterAsync_WhenAnotherRegistrationIsInProgress_ReturnsRegistrationInProgress()
    {
        RepositoryAllowlist allowlist = CreateAllowlist();
        RepositoryRegistrationService service = CreateService(allowlist);
        TaskCompletionSource<ApprovalPromptOutcome> pendingApproval = new();
        _approvalPrompt.RequestApprovalAsync(
                Arg.Any<ApprovalPromptRequest>(), Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>())
            .Returns(_ => pendingApproval.Task);

        Task<RepositoryRegistrationOutcome> firstCall = service.RegisterAsync(
            "new-repo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);
        while (!_approvalPrompt.ReceivedCalls().Any())
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        RepositoryRegistrationOutcome secondOutcome = await service.RegisterAsync(
            "another-repo", LocalRoot, "origin", "develop", "main", TestContext.Current.CancellationToken);

        secondOutcome.IsSuccess.Should().BeFalse();
        secondOutcome.Error!.Code.Should().Be("registration_in_progress");

        pendingApproval.SetResult(ApprovalPromptOutcome.Denied());
        await firstCall;
    }

    private RepositoryAllowlist CreateAllowlist() => new(new BuckettieOptions
    {
        AtlassianEmail = "developer@example.com",
        BitbucketUsername = "developer",
        Repositories = new Dictionary<string, RepositoryOptions>(),
    });

    private RepositoryRegistrationService CreateService(RepositoryAllowlist allowlist, string? configurationPath = null)
    {
        BuckettieOptions options = new()
        {
            AtlassianEmail = "developer@example.com",
            BitbucketUsername = "developer",
            Repositories = allowlist.Snapshot(),
        };
        RepositoryRegistrationValidator validator = new(allowlist, _environment, _git);
        return new RepositoryRegistrationService(
            validator,
            allowlist,
            options,
            new JsonBuckettieOptionsLoader(),
            _approvalPrompt,
            configurationPath ?? _configurationPath);
    }
}
