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

    private string WriteConfiguration()
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "buckettie.json");
        File.WriteAllText(path, """
            {
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
            """);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
