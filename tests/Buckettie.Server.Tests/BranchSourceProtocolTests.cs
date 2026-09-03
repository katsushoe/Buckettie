using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Git;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class BranchSourceProtocolTests
{
    [Theory]
    [InlineData("codex")]
    [InlineData("claude")]
    public async Task HttpContract_ForClientIdentity_RequiresSourceAndPreservesResults(string clientName)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        const string hash = "0123456789abcdef0123456789abcdef01234567";
        IBitbucketRepositoryGateway branches = Substitute.For<IBitbucketRepositoryGateway>();
        branches.CreateBranchAsync("example", "develop", "main", Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Success(new("develop", hash, "main", "branch", hash)));
        branches.CreateBranchAsync("example", "develop", " ", Arg.Any<CancellationToken>())
            .Returns(BitbucketResult<BitbucketBranchInfo>.Failure(BitbucketError.InvalidBranchSource));
        IGitGateway git = Substitute.For<IGitGateway>();
        git.GetStatusAsync("example", Arg.Any<CancellationToken>()).Returns(GitGatewayResult.Success(
            "status", "example", "main", new("example", "main", hash, null, hash, null, null, true,
                "refs/remotes/origin/develop", "remote_tracking_ref_missing_or_not_fetched", ["refs/remotes/origin/develop"])));
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        builder.Services.AddSingleton(branches);
        builder.Services.AddSingleton(git);
        builder.Services.AddSingleton(Substitute.For<IRepositoryRegistrationService>());
        builder.Services.AddSingleton(Substitute.For<IRepositoryUnregistrationService>());
        builder.Services.AddSingleton(Substitute.For<IRepositoryUpdateService>());
        builder.Services.AddMcpServer().WithHttpTransport(options => options.Stateless = true)
            .WithTools<BuckettieMcpTools>(BuckettieMcpJson.CreateOptions());
        await using WebApplication app = builder.Build();
        app.MapMcp("/mcp");
        await app.StartAsync(cancellationToken);
        using HttpClient client = new() { BaseAddress = new Uri(app.Urls.Single()), Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");

        using JsonDocument initialized = await SendAsync(client, "initialize", new
        {
            protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = clientName, version = "test" },
        }, cancellationToken);
        initialized.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString().Should().NotBeNullOrEmpty();
        client.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2025-06-18");
        using JsonDocument listed = await SendAsync(client, "tools/list", new { }, cancellationToken);
        JsonElement tool = listed.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Single(item => item.GetProperty("name").GetString() == "bitbucket_branch_create");
        tool.GetProperty("inputSchema").GetProperty("required").EnumerateArray().Select(item => item.GetString())
            .Should().Contain("source");
        tool.GetProperty("annotations").GetProperty("destructiveHint").GetBoolean().Should().BeTrue();

        using JsonDocument missing = await SendAsync(client, "tools/call", new
        {
            name = "bitbucket_branch_create", arguments = new { repository = "example", branch = "develop" },
        }, cancellationToken);
        bool rejected = missing.RootElement.TryGetProperty("error", out _)
            || missing.RootElement.GetProperty("result").GetProperty("isError").GetBoolean();
        rejected.Should().BeTrue();
        branches.ReceivedCalls().Should().BeEmpty();

        using JsonDocument created = await SendAsync(client, "tools/call", new
        {
            name = "bitbucket_branch_create", arguments = new { repository = "example", branch = "develop", source = "main" },
        }, cancellationToken);
        JsonElement createdData = created.RootElement.GetProperty("result").GetProperty("structuredContent");
        createdData.GetProperty("ok").GetBoolean().Should().BeTrue();
        createdData.GetProperty("data").GetProperty("source_hash").GetString().Should().Be(hash);

        using JsonDocument invalid = await SendAsync(client, "tools/call", new
        {
            name = "bitbucket_branch_create", arguments = new { repository = "example", branch = "develop", source = " " },
        }, cancellationToken);
        invalid.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("error")
            .GetProperty("code").GetString().Should().Be("branch_source_invalid");
        using JsonDocument status = await SendAsync(client, "tools/call", new
        {
            name = "bitbucket_repository_status", arguments = new { repository = "example" },
        }, cancellationToken);
        JsonElement statusData = status.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("data").GetProperty("status");
        statusData.GetProperty("ahead").ValueKind.Should().Be(JsonValueKind.Null);
        statusData.GetProperty("local_head").GetString().Should().Be(hash);
        await app.StopAsync(cancellationToken);
    }

    private static async Task<JsonDocument> SendAsync(HttpClient client, string method, object parameters, CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0", id = 1, method, @params = parameters,
        }, cancellationToken);
        response.EnsureSuccessStatusCode();
        string body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
        {
            body = body.Split('\n').First(line => line.StartsWith("data: ", StringComparison.Ordinal))[6..];
        }
        return JsonDocument.Parse(body);
    }
}
