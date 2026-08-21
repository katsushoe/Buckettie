using FluentAssertions;
using Xunit;

namespace Buckettie.Cli.Tests;

public sealed class CliApplicationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"buckettie-cli-{Guid.NewGuid():N}");

    [Fact]
    public async Task Help_WhenConfigDoesNotExist_DoesNotLoadConfiguration()
    {
        StringWriter output = new();
        int exitCode = await CliApplication.RunAsync(["help"], output, new StringWriter(), TestContext.Current.CancellationToken);
        exitCode.Should().Be(0);
        output.ToString().Should().Contain("buckettie doctor");
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
