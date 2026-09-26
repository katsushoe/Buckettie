using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Git;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class ProviderAuthenticationProtocolTests
{
    [Fact]
    public async Task Http_KeyRevokedAfterAuthentication_ReturnsToolErrorWithoutExecuting()
    {
        using ProviderAuthenticationTests.Fixture fixture = new();
        IGitGateway git = Substitute.For<IGitGateway>();
        WebApplicationBuilder builder = Builder(git, fixture.Options);
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        await using WebApplication app = builder.Build();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        app.Use(boundary.InvokeAsync);
        app.Use((context, next) =>
        {
            fixture.WriteTrust(Moyai.ProviderAuthentication.SigningKeyState.Revoked);
            return next(context);
        });
        app.MapMcp("/mcp");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = Client(app.Urls.Single());
        using JsonDocument result = await Send(client, "tools/call",
            new { name = "bitbucket_push", arguments = new { repository = "example" } }, fixture.Sign());
        result.RootElement.GetProperty("result").GetProperty("isError").GetBoolean().Should().BeTrue();
        result.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString()
            .Should().Contain("auth_key_revoked");
        git.ReceivedCalls().Should().BeEmpty();
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("codex")]
    [InlineData("claude")]
    public async Task Http_AuthenticatedReadWriteAndError_PreservesMcpContract(string clientName)
    {
        using ProviderAuthenticationTests.Fixture fixture = new();
        IGitGateway git = Substitute.For<IGitGateway>();
        git.PushAsync("example", Arg.Any<CancellationToken>()).Returns(GitGatewayResult.Success("push", "example", "develop"));
        git.GetDiffAsync("example", Arg.Any<CancellationToken>()).Returns(GitGatewayResult.Success("diff", "example", "develop"));
        WebApplicationBuilder builder = Builder(git, fixture.Options);
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        await using WebApplication app = builder.Build();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        app.Use(boundary.InvokeAsync);
        app.MapMcp("/mcp");
        await app.StartAsync(TestContext.Current.CancellationToken);
        using HttpClient client = Client(app.Urls.Single());
        using JsonDocument initialized = await Send(client, "initialize", new
        { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = clientName, version = "test" } });
        initialized.RootElement.GetProperty("result").GetProperty("protocolVersion").GetString().Should().NotBeNullOrEmpty();
        client.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2025-06-18");
        using JsonDocument discovery = await Send(client, "tools/list", new { });
        discovery.RootElement.GetProperty("result").GetProperty("tools").EnumerateArray().Should().NotBeEmpty();
        using JsonDocument capabilities = await Send(client, "tools/call", new { name = "bitbucket_provider_capabilities", arguments = new { } });
        capabilities.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("data")
            .GetProperty("authentication").GetProperty("required_audience").GetString().Should().Be("buckettie");
        using JsonDocument denied = await Send(client, "tools/call", new { name = "list_projects", arguments = new { } }, status: HttpStatusCode.Unauthorized);
        denied.RootElement.GetProperty("error").GetProperty("message").GetString().Should().Be("auth_administrator_required");
        using JsonDocument pushed = await Send(client, "tools/call", new { name = "bitbucket_push", arguments = new { repository = "example" } }, fixture.Sign());
        pushed.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("ok").GetBoolean().Should().BeTrue();
        using JsonDocument read = await Send(client, "tools/call", new { name = "bitbucket_repository_diff", arguments = new { repository = "example" } },
            fixture.Sign(new() { ["scope"] = new[] { "repository.read" } }));
        read.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("ok").GetBoolean().Should().BeTrue();
        using JsonDocument error = await Send(client, "tools/call", new { name = "bitbucket_push", arguments = new { repository = "example" } },
            fixture.Sign(new() { ["scope"] = new[] { "repository.read" } }), HttpStatusCode.Unauthorized);
        error.RootElement.GetProperty("error").GetProperty("message").GetString().Should().Be("auth_scope_denied");
        await git.Received(1).PushAsync("example", Arg.Any<CancellationToken>());
        await git.Received(1).GetDiffAsync("example", Arg.Any<CancellationToken>());
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Http_StandaloneWithoutMoyai_AllowsReadsAndChangesFromLoopback()
    {
        using ProviderAuthenticationTests.Fixture fixture = new();
        IGitGateway git = Substitute.For<IGitGateway>();
        git.GetDiffAsync("example", Arg.Any<CancellationToken>()).Returns(GitGatewayResult.Success("diff", "example", "develop"));
        git.PushAsync("example", Arg.Any<CancellationToken>()).Returns(GitGatewayResult.Success("push", "example", "develop"));
        git.FetchAsync("example", Arg.Any<CancellationToken>()).Returns(GitGatewayResult.Success("fetch", "example", "develop"));
        WebApplicationBuilder builder = Builder(git, fixture.Options);
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        await using WebApplication app = builder.Build();
        ProviderAuthenticationBoundary? boundary = null;
        app.Use((context, next) => boundary!.InvokeAsync(context, next));
        app.MapMcp("/mcp");
        await app.StartAsync(TestContext.Current.CancellationToken);
        BuckettieOptions options = fixture.Options with
        { McpPort = new Uri(app.Urls.Single()).Port, ProviderAuthentication = null };
        boundary = await ProviderAuthenticationBoundary.CreateAsync(options, new RepositoryAllowlist(options),
            NullLogger<ProviderAuthenticationBoundary>.Instance, TestContext.Current.CancellationToken);
        using HttpClient client = Client(app.Urls.Single());
        foreach (string tool in new[] { "bitbucket_repository_diff", "bitbucket_fetch", "bitbucket_push" })
        {
            using JsonDocument result = await Send(client, "tools/call", new { name = tool, arguments = new { repository = "example" } });
            result.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("ok").GetBoolean().Should().BeTrue();
        }
        // Moyai attaches an assertion to tools/call; standalone mode ignores it instead of rejecting the call.
        using JsonDocument credential = await Send(client, "tools/call", new { name = "bitbucket_push", arguments = new { repository = "example" } },
            "moyai-assertion");
        credential.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("ok").GetBoolean().Should().BeTrue();
        // Same shape the MCP C# SDK 2.2 client (used by Moyai) sends before any tool call.
        Dictionary<string, object> discoverMeta = new()
        {
            ["io.modelcontextprotocol/protocolVersion"] = "2026-07-28",
            ["io.modelcontextprotocol/clientInfo"] = new { name = "moyai", version = "test" },
            ["io.modelcontextprotocol/clientCapabilities"] = new { },
        };
        using HttpRequestMessage discoverRequest = new(HttpMethod.Post, "/mcp")
        {
            Content = JsonContent.Create(new { jsonrpc = "2.0", id = 9, method = "server/discover",
                @params = new Dictionary<string, object> { ["_meta"] = discoverMeta } }),
        };
        using HttpResponseMessage discoverResponse = await client.SendAsync(discoverRequest, TestContext.Current.CancellationToken);
        // The boundary must hand discovery to the MCP handler instead of answering 401 itself.
        discoverResponse.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
        (await discoverResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken))
            .Should().NotContain("mcp_method_not_allowed").And.NotContain("auth_scope_denied");
        await git.Received(2).PushAsync("example", Arg.Any<CancellationToken>());
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(true)]
    public async Task Http_DirectLoopbackConnection_AllowsReadsButRequiresAssertionForChanges(bool configured)
    {
        using ProviderAuthenticationTests.Fixture fixture = new();
        IGitGateway git = Substitute.For<IGitGateway>();
        git.GetDiffAsync("example", Arg.Any<CancellationToken>()).Returns(GitGatewayResult.Success("diff", "example", "develop"));
        WebApplicationBuilder builder = Builder(git, fixture.Options);
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        await using WebApplication app = builder.Build();
        ProviderAuthenticationBoundary? boundary = null;
        app.Use((context, next) => boundary!.InvokeAsync(context, next));
        app.MapMcp("/mcp");
        await app.StartAsync(TestContext.Current.CancellationToken);
        BuckettieOptions options = fixture.Options with
        {
            McpPort = new Uri(app.Urls.Single()).Port,
            ProviderAuthentication = configured ? fixture.Options.ProviderAuthentication : null,
        };
        boundary = await ProviderAuthenticationBoundary.CreateAsync(options, new RepositoryAllowlist(options),
            NullLogger<ProviderAuthenticationBoundary>.Instance, TestContext.Current.CancellationToken, moyaiIntegration: true);
        using HttpClient client = Client(app.Urls.Single());
        using JsonDocument listed = await Send(client, "tools/call", new { name = "list_projects", arguments = new { } });
        listed.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("ok").GetBoolean().Should().BeTrue();
        using JsonDocument read = await Send(client, "tools/call", new { name = "bitbucket_repository_diff", arguments = new { repository = "example" } });
        read.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("ok").GetBoolean().Should().BeTrue();
        string missing = configured ? "auth_assertion_missing" : "authentication_unavailable";
        foreach (string tool in new[] { "bitbucket_push", "bitbucket_fetch", "bitbucket_pull" })
        {
            using JsonDocument denied = await Send(client, "tools/call", new { name = tool, arguments = new { repository = "example" } },
                status: HttpStatusCode.Unauthorized);
            denied.RootElement.GetProperty("error").GetProperty("message").GetString().Should().Be(missing);
        }
        using JsonDocument invalid = await Send(client, "tools/call", new { name = "bitbucket_repository_diff", arguments = new { repository = "example" } },
            "old-service-token", HttpStatusCode.Unauthorized);
        invalid.RootElement.GetProperty("error").GetProperty("message").GetString()
            .Should().Be(configured ? "auth_assertion_invalid" : "authentication_unavailable");
        git.ReceivedCalls().Should().ContainSingle();
        await git.Received(1).GetDiffAsync("example", Arg.Any<CancellationToken>());
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Https_AdministratorCertificate_AllowsManagementButCannotReplaceAssertion()
    {
        using ProviderAuthenticationTests.Fixture fixture = new();
        using RSA key = RSA.Create(2048);
        CertificateRequest request = new("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using X509Certificate2 generated = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddHours(1));
        // Schannel requires an imported key handle for these disposable HTTPS fixtures.
        using X509Certificate2 certificate = X509CertificateLoader.LoadPkcs12(generated.Export(X509ContentType.Pfx), null);
        IGitGateway git = Substitute.For<IGitGateway>();
        WebApplicationBuilder builder = Builder(git, fixture.Options);
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0, listen => listen.UseHttps(https =>
        {
            https.ServerCertificate = certificate;
            https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
            https.ClientCertificateValidation = (peer, _, _) => AdministratorCertificates.IsAllowed(peer, [certificate.Thumbprint]);
        })));
        await using WebApplication app = builder.Build();
        ProviderAuthenticationBoundary? boundary = null;
        app.Use((context, next) => boundary!.InvokeAsync(context, next));
        app.MapMcp("/mcp");
        await app.StartAsync(TestContext.Current.CancellationToken);
        BuckettieOptions configured = fixture.Options with { AdministratorEndpoint = new()
        { Port = new Uri(app.Urls.Single()).Port, ServerCertificateThumbprint = certificate.Thumbprint, ClientCertificateThumbprints = [certificate.Thumbprint] } };
        boundary = await ProviderAuthenticationBoundary.CreateAsync(configured, new RepositoryAllowlist(configured),
            NullLogger<ProviderAuthenticationBoundary>.Instance, TestContext.Current.CancellationToken, moyaiIntegration: true);
        using HttpClientHandler handler = new();
        handler.ClientCertificates.Add(certificate);
        handler.ServerCertificateCustomValidationCallback = (_, peer, _, _) => AdministratorCertificates.IsAllowed(peer, [certificate.Thumbprint]);
        using HttpClient client = Client(app.Urls.Single(), handler);
        using JsonDocument listed = await Send(client, "tools/call", new { name = "list_projects", arguments = new { } });
        listed.RootElement.GetProperty("result").GetProperty("structuredContent").GetProperty("ok").GetBoolean().Should().BeTrue();
        using JsonDocument denied = await Send(client, "tools/call", new { name = "bitbucket_push", arguments = new { repository = "example" } }, status: HttpStatusCode.Unauthorized);
        denied.RootElement.GetProperty("error").GetProperty("message").GetString().Should().Be("auth_assertion_missing");
        git.ReceivedCalls().Should().BeEmpty();
        using HttpClientHandler anonymousHandler = new();
        anonymousHandler.ServerCertificateCustomValidationCallback = (_, peer, _, _) =>
            AdministratorCertificates.IsAllowed(peer, [certificate.Thumbprint]);
        using HttpClient anonymous = Client(app.Urls.Single(), anonymousHandler);
        await Assert.ThrowsAsync<HttpRequestException>(() => Send(anonymous, "tools/call", new { name = "list_projects", arguments = new { } }));
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    private static WebApplicationBuilder Builder(IGitGateway git, BuckettieOptions options)
    {
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(new RepositoryAllowlist(options));
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddSingleton<IGitGateway>(provider => new AuditedGitGateway(git, Substitute.For<IBuckettieAuditLogger>(),
            tool => ProviderAuthenticationBoundary.EnsureCurrentAsync(provider.GetRequiredService<Microsoft.AspNetCore.Http.IHttpContextAccessor>().HttpContext, tool)));
        builder.Services.AddSingleton(Substitute.For<IBitbucketRepositoryGateway>());
        builder.Services.AddSingleton(Substitute.For<IRepositoryRegistrationService>());
        builder.Services.AddSingleton(Substitute.For<IRepositoryUnregistrationService>());
        builder.Services.AddSingleton(Substitute.For<IRepositoryUpdateService>());
        builder.Services.AddMcpServer().WithHttpTransport(transport => transport.Stateless = true)
            .WithTools<BuckettieMcpTools>(BuckettieMcpJson.CreateOptions());
        return builder;
    }

    private static HttpClient Client(string address, HttpMessageHandler? handler = null)
    {
        HttpClient client = handler is null ? new() : new(handler, false);
        client.BaseAddress = new Uri(address);
        client.Timeout = TimeSpan.FromSeconds(15);
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json, text/event-stream");
        return client;
    }

    private static async Task<JsonDocument> Send(HttpClient client, string method, object parameters, string? assertion = null,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        using HttpRequestMessage request = new(HttpMethod.Post, "/mcp")
        { Content = JsonContent.Create(new { jsonrpc = "2.0", id = 1, method, @params = parameters }) };
        if (assertion is not null)
        {
            request.Headers.Authorization = new("Bearer", assertion);
            request.Headers.Add("X-Moyai-Operation-Id", "operation-1");
        }
        using HttpResponseMessage response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        response.StatusCode.Should().Be(status);
        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (assertion is not null) body.Should().NotContain(assertion);
        if (response.Content.Headers.ContentType?.MediaType == "text/event-stream")
            body = body.Split('\n').First(line => line.StartsWith("data: ", StringComparison.Ordinal))[6..];
        return JsonDocument.Parse(body);
    }
}
