using FluentAssertions;
using Xunit;
using Buckettie.Application.Configuration;
using Buckettie.Application.Credentials;
using Buckettie.Application.Git;
using Buckettie.Infrastructure.Git;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Buckettie.Cli.Tests;

public sealed class CliApplicationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"buckettie-cli-{Guid.NewGuid():N}");

    [Fact]
    public void IsToolCallSuccessful_WhenResponseIsServerSentEvent_ParsesDataPayload()
    {
        const string response = "event: message\ndata: {\"result\":{\"structuredContent\":{\"ok\":true}},\"id\":1,\"jsonrpc\":\"2.0\"}\n\n";

        bool success = CliApplication.IsToolCallSuccessful(response);

        success.Should().BeTrue();
    }

    [Fact]
    public async Task Help_WhenConfigDoesNotExist_DoesNotLoadConfiguration()
    {
        StringWriter output = new();
        int exitCode = await CliApplication.RunAsync(["help"], output, new StringWriter(), TestContext.Current.CancellationToken);
        exitCode.Should().Be(0);
        output.ToString().Should()
            .Contain("buckettie doctor")
            .And.Contain("buckettie auth set <repository> [--console-token]")
            .And.Contain("buckettie repo diff <repository>")
            .And.Contain("buckettie repo commit <repository> <message>");
    }

    [Fact]
    public async Task ConfigCheck_WhenConfigurationIsValid_ReturnsSuccess()
    {
        string path = WriteConfiguration();
        StringWriter output = new();
        int exitCode = await CliApplication.RunAsync(["--config", path, "config", "check"], output, new StringWriter(), TestContext.Current.CancellationToken);
        exitCode.Should().Be(0);
        output.ToString().Should().Contain("[OK] Config");
    }

    [Theory]
    [InlineData("ja-JP", "[OK] 設定")]
    [InlineData("en-US", "[OK] Config")]
    public async Task ConfigCheck_WhenLanguageIsConfigured_LocalizesOutput(string language, string expected)
    {
        string path = WriteConfiguration(language);
        StringWriter output = new();

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "config", "check"], output, new StringWriter(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain(expected);
    }

    [Fact]
    public async Task Help_WhenLanguageIsJapanese_ReturnsJapaneseGuidance()
    {
        string path = WriteConfiguration("ja-JP");
        StringWriter output = new();

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "help"], output, new StringWriter(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("使用方法:").And.Contain("共通オプション");
    }

    [Fact]
    public async Task McpStatus_WhenLanguageIsJapanese_LocalizesEndpointLabel()
    {
        string path = WriteConfiguration("ja-JP");
        StringWriter output = new();

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "mcp", "status"], output, new StringWriter(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        output.ToString().Should().Contain("[NG] MCP エンドポイント");
    }

    [Fact]
    public async Task RepositoryDiff_WhenProviderIsStopped_ReturnsFailureWithoutThrowing()
    {
        string path = WriteConfiguration();
        StringWriter output = new();

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "repo", "diff", "example"],
            output,
            new StringWriter(),
            TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        output.ToString().Should().Contain("[NG] bitbucket_repository_diff");
    }

    [Fact]
    public async Task ConfigCheck_WhenJsonIsInvalid_DoesNotEchoInput()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "invalid.json");
        await File.WriteAllTextAsync(path, "{ secret-token }", TestContext.Current.CancellationToken);
        StringWriter error = new();
        int exitCode = await CliApplication.RunAsync(["--config", path, "config", "check"], new StringWriter(), error, TestContext.Current.CancellationToken);
        exitCode.Should().Be(2);
        error.ToString().Should().Contain("InvalidJson").And.NotContain("secret-token");
    }

    [Fact]
    public async Task ServiceStatus_WhenServiceIsRunning_ReturnsStableStatus()
    {
        FakeServiceCommandExecutor executor = new(new(0, "STATE : 4 RUNNING"));
        StringWriter output = new();
        int exitCode = await CliApplication.RunAsync(["service", "status"], output, new StringWriter(),
            TestContext.Current.CancellationToken, executor);
        exitCode.Should().Be(0);
        output.ToString().Should().Contain("[OK] Service: Running");
        executor.Arguments.Should().Equal("query", "Buckettie");
    }

    [Fact]
    public async Task ServiceStatus_WhenLanguageIsJapanese_ReturnsJapaneseStatus()
    {
        string path = WriteConfiguration("ja-JP");
        FakeServiceCommandExecutor executor = new(new(0, "STATE : 4 RUNNING"));
        StringWriter output = new();

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "service", "status"], output, new StringWriter(),
            TestContext.Current.CancellationToken, executor);

        exitCode.Should().Be(0);
        output.ToString().Should().Contain("[OK] サービス: 実行中");
    }

    [Fact]
    public async Task Start_WhenServiceControlFails_DoesNotExposeNativeOutput()
    {
        FakeServiceCommandExecutor executor = new(new(5, "sensitive native diagnostic"));
        StringWriter output = new();
        int exitCode = await CliApplication.RunAsync(["start"], output, new StringWriter(),
            TestContext.Current.CancellationToken, executor);
        exitCode.Should().Be(1);
        output.ToString().Should().Contain("[NG] Service").And.NotContain("sensitive");
        executor.Arguments.Should().Equal("start", "Buckettie");
    }

    [Theory]
    [InlineData(false, "Administrator privileges are required", "elevated terminal")]
    [InlineData(true, "管理者権限が必要", "管理者として実行")]
    public async Task Restart_AccessDenied_ExplainsCauseAndRemedy(bool japanese, string cause, string remedy)
    {
        FakeServiceCommandExecutor executor = new(new(5, "sensitive native diagnostic"));
        StringWriter output = new();
        string[] arguments = japanese ? ["--config", WriteConfiguration("ja-JP"), "restart"] : ["restart"];
        int exitCode = await CliApplication.RunAsync(arguments, output, new StringWriter(),
            TestContext.Current.CancellationToken, executor);
        exitCode.Should().Be(1);
        output.ToString().Should().Contain(cause).And.Contain(remedy).And.NotContain("sensitive");
        executor.Arguments.Should().Equal("query", "Buckettie");
    }

    [Fact]
    public async Task RepositoryRegister_WhenNoInputOption_UsesGuiTokenPromptByDefault()
    {
        string path = WriteConfiguration();
        WriteGitRepository(_directory);
        string? promptedRepository = null;
        string? promptedRemoteUrl = null;
        string? promptedLanguage = null;

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "repo", "register", "newrepo", _directory],
            new StringWriter(),
            new StringWriter(),
            TestContext.Current.CancellationToken,
            secretReader: () => throw new InvalidOperationException(),
            tokenPrompt: (repository, remoteUrl, language, _) =>
            {
                promptedRepository = repository;
                promptedRemoteUrl = remoteUrl;
                promptedLanguage = language;
                return Task.FromResult<string?>(null);
            });

        exitCode.Should().Be(1);
        promptedRepository.Should().Be("newrepo");
        promptedRemoteUrl.Should().Be("https://bitbucket.org/workspace/repository.git");
        promptedLanguage.Should().Be("en-US");
    }

    [Fact]
    public async Task RepositoryRegister_WhenConsoleTokenOptionIsSpecified_UsesSecretReader()
    {
        string path = WriteConfiguration();
        bool secretReaderCalled = false;

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "repo", "register", "newrepo", "C:\\repo", "--console-token"],
            new StringWriter(),
            new StringWriter(),
            TestContext.Current.CancellationToken,
            secretReader: () =>
            {
                secretReaderCalled = true;
                return null;
            },
            tokenPrompt: (_, _, _, _) => throw new InvalidOperationException());

        exitCode.Should().Be(1);
        secretReaderCalled.Should().BeTrue();
    }

    [Theory]
    [InlineData("success", false, 0, "[OK]")]
    [InlineData("success", true, 0, "[OK]")]
    [InlineData("cancel", false, 1, "TokenPromptCancelled")]
    [InlineData("cancel", true, 1, "TokenPromptCancelled")]
    [InlineData("LaunchFailed", false, 1, "TokenPromptLaunchFailed")]
    [InlineData("save-failure", false, 1, "ProviderFailure")]
    public async Task AuthSet_WhenInputCompletes_ReportsOutcomeWithoutSecret(
        string outcome, bool consoleToken, int expectedExit, string expectedOutput)
    {
        string path = WriteConfiguration();
        WriteGitRepository(_directory);
        BuckettieOptions options = JsonSerializer.Deserialize<BuckettieOptions>(File.ReadAllText(path),
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower })!;
        options = options with { Repositories = new Dictionary<string, RepositoryOptions>
        {
            ["example"] = options.Repositories["example"] with { LocalRoot = _directory },
        } };
        FakeTokenStore store = new(outcome == "save-failure");
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(options).AddSingleton<IApiTokenStore>(store)
            .AddSingleton<IGitCommandClient>(new GitCommandClient(TimeSpan.FromSeconds(5),
                Path.Combine(_directory, "askpass.exe"), "developer"))
            .BuildServiceProvider();
        StringWriter output = new();
        StringWriter error = new();
        const string secret = "test-secret-must-not-be-printed";
        int exit = await CliApplication.SetAuthenticationAsync(services, "example", output, error,
            () => { consoleToken.Should().BeTrue(); return outcome == "cancel" ? null : secret; },
            (repository, remote, language, _) =>
            {
                consoleToken.Should().BeFalse();
                repository.Should().Be("example");
                remote.Should().Be("https://bitbucket.org/workspace/repository.git");
                language.Should().Be("en-US");
                if (Enum.TryParse(outcome, out TokenPromptError failure)) throw new TokenPromptException(failure);
                return Task.FromResult(outcome == "cancel" ? null : secret);
            }, consoleToken, false, TestContext.Current.CancellationToken);
        exit.Should().Be(expectedExit);
        (output.ToString() + error).Should().Contain(expectedOutput).And.NotContain(secret);
        store.SavedToken.Should().Be(outcome is "success" or "save-failure" ? secret : null);
        store.Repository.Should().Be(outcome is "success" or "save-failure" ? "example" : null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AuthSet_WhenRepositoryIsNotRegistered_RejectsBeforePrompting(bool consoleToken)
    {
        string path = WriteConfiguration();
        string[] args = consoleToken
            ? ["--config", path, "auth", "set", "unregistered", "--console-token"]
            : ["--config", path, "auth", "set", "unregistered"];
        StringWriter output = new();
        int exit = await CliApplication.RunAsync(args, output, new StringWriter(),
            TestContext.Current.CancellationToken,
            secretReader: () => throw new InvalidOperationException("Unexpected terminal input"),
            tokenPrompt: (_, _, _, _) => throw new InvalidOperationException("Unexpected GUI input"));
        exit.Should().Be(1);
        output.ToString().Should().Contain("RepositoryNotAllowed");
    }

    private sealed class FakeTokenStore(bool fail) : IApiTokenStore
    {
        public string? SavedToken { get; private set; }
        public string? Repository { get; private set; }
        public ApiTokenStoreResult Save(string repositoryId, string token)
        {
            Repository = repositoryId;
            SavedToken = token;
            return fail ? ApiTokenStoreResult.Failure(ApiTokenStoreError.ProviderFailure) : ApiTokenStoreResult.Success();
        }
        public ApiTokenStoreResult Read(string repositoryId) => throw new NotSupportedException();
        public ApiTokenStoreResult Delete(string repositoryId) => throw new NotSupportedException();
    }

    private string WriteConfiguration(string language = "en-US")
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "buckettie.json");
        File.WriteAllText(path, """
            {
              "language": "LANGUAGE_VALUE",
              "mcp_port": 65534,
              "atlassian_email": "dev@example.com",
              "bitbucket_username": "developer",
              "repositories": {
                "example": {
                  "workspace": "workspace", "slug": "repository", "local_root": "C:\\repo",
                  "remote": "origin", "develop_branch": "develop", "main_branch": "main",
                  "direct_push_branches": ["develop"], "pull_branches": ["develop", "main"],
                  "protected_branches": ["main"], "tag_target_branch": "main",
                  "tag_pattern": "^v[0-9]+\\.[0-9]+\\.[0-9]+$"
                }
              }
            }
            """.Replace("LANGUAGE_VALUE", language, StringComparison.Ordinal));
        return path;
    }

    private static void WriteGitRepository(string root)
    {
        string gitDirectory = Path.Combine(root, ".git");
        Directory.CreateDirectory(gitDirectory);
        Directory.CreateDirectory(Path.Combine(gitDirectory, "objects"));
        Directory.CreateDirectory(Path.Combine(gitDirectory, "refs", "heads"));
        File.WriteAllText(Path.Combine(gitDirectory, "HEAD"), "ref: refs/heads/develop\n");
        File.WriteAllText(Path.Combine(gitDirectory, "config"), """
            [core]
                bare = false
            [remote "origin"]
                url = https://bitbucket.org/workspace/repository.git
            """);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private sealed class FakeServiceCommandExecutor(ServiceCommandResult result) : IServiceCommandExecutor
    {
        public IReadOnlyList<string> Arguments { get; private set; } = [];

        public Task<ServiceCommandResult> ExecuteAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
        {
            Arguments = arguments;
            return Task.FromResult(result);
        }
    }
}
