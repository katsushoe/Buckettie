using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Buckettie.Cli.Tests;

public sealed class RepositoryLifecycleCommandTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), $"buckettie-cli-repo-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    [Fact]
    public async Task RepoRegister_WhenServiceIsNotRunning_ReturnsNgAndDoesNotThrow()
    {
        string path = WriteConfiguration(GetFreePort());
        WriteGitRepository(_directory);
        StringWriter output = new();

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "repo", "register", "example", _directory],
            output,
            new StringWriter(),
            TestContext.Current.CancellationToken,
            tokenPrompt: (_, _, _, _) => Task.FromResult<string?>("test-token"));

        exitCode.Should().Be(1);
        output.ToString().Should().Contain("[NG] bitbucket_repository_register: example")
            .And.NotContain("test-token");
    }

    [Fact]
    public async Task RepoUnregister_WhenServiceIsNotRunning_ReturnsNg()
    {
        string path = WriteConfiguration(GetFreePort());
        StringWriter output = new();

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "repo", "unregister", "example"],
            output, new StringWriter(), TestContext.Current.CancellationToken);

        exitCode.Should().Be(1);
        output.ToString().Should().Contain("[NG] bitbucket_repository_unregister: example");
    }

    [Fact]
    public async Task RepoUpdate_WhenRequiredFlagsAreMissing_ReturnsUsageErrorWithoutCallingService()
    {
        string path = WriteConfiguration(GetFreePort());
        StringWriter error = new();

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "repo", "update", "example"],
            new StringWriter(), error, TestContext.Current.CancellationToken);

        exitCode.Should().Be(2);
        error.ToString().Should().Contain("--direct-push-branches");
    }

    [Fact]
    public async Task RepoUpdate_WhenServiceRespondsOk_PrintsResponseBody()
    {
        int port = GetFreePort();
        string path = WriteConfiguration(port);
        using HttpListener listener = new();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/mcp/");
        listener.Start();
        Task<string> serverTask = RespondOnceAsync(listener, """{"result":{"ok":true}}""");

        StringWriter output = new();
        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "repo", "update", "example",
                "--direct-push-branches", "develop",
                "--pull-branches", "develop,main",
                "--protected-branches", "main",
                "--tag-target-branch", "main",
                "--tag-pattern", "^v[0-9]+.*$"],
            output, new StringWriter(), TestContext.Current.CancellationToken);

        string requestBody = await serverTask;
        exitCode.Should().Be(0);
        output.ToString().Should().Contain("[OK] bitbucket_repository_update: example");
        output.ToString().Should().Contain("\"ok\":true");
        requestBody.Should().Contain("bitbucket_repository_update").And.Contain("tools/call");
    }

    [Theory]
    [InlineData("repo|fetch|example", "bitbucket_fetch", "\"repository\":\"example\"")]
    [InlineData("repo|pull|example", "bitbucket_pull", "\"repository\":\"example\"")]
    [InlineData("repo|push|example", "bitbucket_push", "\"repository\":\"example\"")]
    [InlineData("branch|list|example", "bitbucket_branch_list", "\"repository\":\"example\"")]
    [InlineData("branch|get|example|develop", "bitbucket_branch_get", "\"branch\":\"develop\"")]
    [InlineData("pr|list|example|--state|OPEN|--source|develop|--destination|main", "bitbucket_pr_list", "\"state\":\"OPEN\"")]
    [InlineData("pr|get|example|12", "bitbucket_pr_get", "\"pullRequestId\":12")]
    [InlineData("pr|diff|example|12", "bitbucket_pr_diff", "\"pullRequestId\":12")]
    [InlineData("pr|create|example|Title|Description|--draft", "bitbucket_pr_create", "\"draft\":true")]
    [InlineData("pr|merge|example|12|--strategy|Squash|--message|Done", "bitbucket_pr_merge", "\"strategy\":\"Squash\"")]
    [InlineData("tag|list|example", "bitbucket_tag_list", "\"repository\":\"example\"")]
    [InlineData("tag|get|example|v1.0.0", "bitbucket_tag_get", "\"tag\":\"v1.0.0\"")]
    [InlineData("tag|create|example|v1.0.0|--message|Release", "bitbucket_tag_create", "\"message\":\"Release\"")]
    [InlineData("mcp|version", "get_version", "\"arguments\":{}")]
    [InlineData("branch|create|example|develop|main", "bitbucket_branch_create", "\"source\":\"main\"")]
    [InlineData("branch|create|example|feature/test|0123456789abcdef0123456789abcdef01234567", "bitbucket_branch_create", "\"source\":\"0123456789abcdef0123456789abcdef01234567\"")]
    public async Task McpEquivalentCommand_CallsExpectedTool(
        string serializedArguments, string expectedTool, string expectedArgument)
    {
        int port = GetFreePort();
        string path = WriteConfiguration(port);
        using HttpListener listener = new();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/mcp/");
        listener.Start();
        Task<string> serverTask = RespondOnceAsync(listener, """{"result":{"ok":true}}""");

        StringWriter output = new();
        string[] arguments = serializedArguments.Split('|');
        int exitCode = await CliApplication.RunAsync(
            ["--config", path, .. arguments], output, new StringWriter(), TestContext.Current.CancellationToken);

        string requestBody = await serverTask;
        exitCode.Should().Be(0);
        output.ToString().Should().Contain($"[OK] {expectedTool}");
        requestBody.Should().Contain($"\"name\":\"{expectedTool}\"").And.Contain(expectedArgument);
    }

    [Theory]
    [InlineData("get")]
    [InlineData("diff")]
    [InlineData("merge")]
    public async Task PullRequestCommand_WhenIdIsInvalid_ReturnsUsageError(string operation)
    {
        string path = WriteConfiguration(GetFreePort());
        StringWriter error = new();

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "pr", operation, "example", "invalid"],
            new StringWriter(), error, TestContext.Current.CancellationToken);

        exitCode.Should().Be(2);
        error.ToString().Should().MatchRegex("positive integer|正の整数");
    }

    [Fact]
    public async Task McpEquivalentCommand_WhenStructuredToolResultFails_ReturnsFailure()
    {
        int port = GetFreePort();
        string path = WriteConfiguration(port);
        using HttpListener listener = new();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/mcp/");
        listener.Start();
        Task<string> serverTask = RespondOnceAsync(listener,
            """{"result":{"structuredContent":{"ok":false,"error":{"code":"rejected"}}}}""");
        StringWriter output = new();

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "repo", "push", "example"],
            output, new StringWriter(), TestContext.Current.CancellationToken);

        await serverTask;
        exitCode.Should().Be(1);
        output.ToString().Should().Contain("[NG] bitbucket_push: example");
    }

    [Fact]
    public async Task BranchCreate_WhenSourceIsOmitted_ReturnsUsageErrorWithoutService()
    {
        string path = WriteConfiguration(GetFreePort());
        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "branch", "create", "example", "develop"],
            new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken);
        exitCode.Should().Be(2);
    }

    [Fact]
    public async Task BranchCreate_WhenSourceIsRejected_PreservesProviderErrorAndExitCode()
    {
        int port = GetFreePort();
        string path = WriteConfiguration(port);
        using HttpListener listener = new();
        listener.Prefixes.Add($"http://127.0.0.1:{port}/mcp/");
        listener.Start();
        Task<string> serverTask = RespondOnceAsync(listener,
            """{"result":{"structuredContent":{"ok":false,"error":{"code":"branch_source_invalid"}}}}""");
        StringWriter output = new();

        int exitCode = await CliApplication.RunAsync(
            ["--config", path, "branch", "create", "example", "develop", " "],
            output, new StringWriter(), TestContext.Current.CancellationToken);

        string request = await serverTask;
        request.Should().Contain("\"source\":\" \"");
        exitCode.Should().Be(1);
        output.ToString().Should().Contain("branch_source_invalid");
    }

    private static async Task<string> RespondOnceAsync(HttpListener listener, string responseBody)
    {
        HttpListenerContext context = await listener.GetContextAsync();
        using StreamReader reader = new(context.Request.InputStream, Encoding.UTF8);
        string requestBody = await reader.ReadToEndAsync();
        byte[] bytes = Encoding.UTF8.GetBytes(responseBody);
        context.Response.ContentType = "application/json";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.OutputStream.Close();
        return requestBody;
    }

    private static int GetFreePort()
    {
        using TcpListener probe = new(IPAddress.Loopback, 0);
        probe.Start();
        int port = ((IPEndPoint)probe.LocalEndpoint).Port;
        probe.Stop();
        return port;
    }

    private string WriteConfiguration(int mcpPort)
    {
        Directory.CreateDirectory(_directory);
        string path = Path.Combine(_directory, "buckettie.json");
        File.WriteAllText(path, $$"""
            {
              "mcp_port": {{mcpPort}},
              "atlassian_email": "dev@example.com",
              "bitbucket_username": "developer",
              "repositories": {}
            }
            """);
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
}
