using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moyai.ProviderAuthentication;
using NSubstitute;
using Buckettie.Application.Git;
using Buckettie.Application.Bitbucket;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class ProviderAuthenticationTests
{
    [Fact]
    public async Task Boundary_ValidAssertion_PassesPrincipalAndRemovesCredential()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        DefaultHttpContext context = fixture.Request(fixture.Sign());
        bool called = false;
        await boundary.InvokeAsync(context, current =>
        {
            called = true;
            current.Items[typeof(AssertionPrincipal)].Should().BeOfType<AssertionPrincipal>();
            current.Request.Headers.Authorization.Should().BeEmpty();
            return Task.CompletedTask;
        });
        called.Should().BeTrue();
        context.Response.Headers["X-Moyai-Operation-Id"].ToString().Should().Be("operation-1");
    }

    [Theory]
    [InlineData("aud", "githubie", "auth_audience_mismatch")]
    [InlineData("prv", "githubie", "auth_audience_mismatch")]
    [InlineData("iss", "moyai:other", "auth_assertion_invalid")]
    [InlineData("project", "22222222-2222-4222-8222-222222222222", "auth_project_mismatch")]
    [InlineData("repository", "bitbucket.org/workspace/other", "auth_project_mismatch")]
    [InlineData("operation_id", "other-operation", "auth_project_mismatch")]
    [InlineData("protocol_version", "2", "auth_protocol_unsupported")]
    public async Task Boundary_WrongClaim_RejectsWithoutDispatch(string claim, string value, string expected)
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        string assertion = fixture.Sign(new() { [claim] = value });
        await AssertRejected(boundary, fixture.Request(assertion), expected, assertion);
    }

    [Theory]
    [InlineData("scope", "auth_scope_denied")]
    [InlineData("expired", "auth_assertion_expired")]
    [InlineData("future", "auth_assertion_not_yet_valid")]
    [InlineData("unsigned", "auth_assertion_invalid")]
    [InlineData("unknown-key", "auth_key_unknown")]
    public async Task Boundary_InvalidCredential_RejectsWithoutDispatch(string scenario, string expected)
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        Dictionary<string, object> changes = new();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (scenario == "scope") changes["scope"] = new[] { "repository.read" };
        if (scenario == "expired") { changes["iat"] = now - 600; changes["nbf"] = now - 600; changes["exp"] = now - 480; }
        if (scenario == "future") { changes["iat"] = now + 600; changes["nbf"] = now + 600; changes["exp"] = now + 720; }
        string assertion = fixture.Sign(changes, scenario == "unsigned" ? "none" : "ES256",
            scenario == "unknown-key" ? "unknown" : "key-1");
        await AssertRejected(boundary, fixture.Request(assertion), expected, assertion);
    }

    [Fact]
    public async Task Boundary_TrustRevokedBetweenRequests_RejectsImmediately()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        await boundary.InvokeAsync(fixture.Request(fixture.Sign()), _ => Task.CompletedTask);
        fixture.WriteTrust(SigningKeyState.Revoked);
        await AssertRejected(boundary, fixture.Request(fixture.Sign()), "auth_key_revoked");
    }

    [Fact]
    public async Task Boundary_ConcurrentConsumersAndReopen_OnlyOneDispatch()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary first = await fixture.CreateAsync();
        ProviderAuthenticationBoundary second = await fixture.CreateAsync();
        string token = fixture.Sign();
        int dispatches = 0;
        await Task.WhenAll(first.InvokeAsync(fixture.Request(token), _ => { Interlocked.Increment(ref dispatches); return Task.CompletedTask; }),
            second.InvokeAsync(fixture.Request(token), _ => { Interlocked.Increment(ref dispatches); return Task.CompletedTask; }));
        dispatches.Should().Be(1);
        ProviderAuthenticationBoundary reopened = await fixture.CreateAsync();
        await AssertRejected(reopened, fixture.Request(token), "auth_replay_detected");
    }

    [Fact]
    public async Task Boundary_MissingTrustFile_FailsClosed()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        File.Delete(fixture.Options.ProviderAuthentication!.TrustBundlePath);
        await AssertRejected(boundary, fixture.Request(fixture.Sign()), "authentication_unavailable");
    }

    [Theory]
    [InlineData("list_projects")]
    [InlineData("bitbucket_repository_register")]
    [InlineData("bitbucket_repository_update")]
    [InlineData("bitbucket_repository_unregister")]
    public async Task Boundary_ManagementWithoutCertificate_RejectsEvenWithAssertion(string tool)
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        await AssertRejected(boundary, fixture.Request(fixture.Sign(), tool), "auth_administrator_required");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("old-service-token")]
    public async Task Boundary_MissingOrStaticToken_Rejects(string? token)
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        await AssertRejected(boundary, fixture.Request(token), token is null ? "auth_assertion_missing" : "auth_assertion_invalid");
    }

    [Fact]
    public async Task Boundary_DuplicateRepositoryArgument_RejectsAmbiguousBody()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        DefaultHttpContext context = fixture.Request(fixture.Sign());
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("""
            {"jsonrpc":"2.0","id":1,"method":"tools/call","params":{"name":"bitbucket_push","arguments":{"repository":"example","repository":"other"}}}
            """));
        await AssertRejected(boundary, context, "auth_assertion_invalid");
    }

    [Fact]
    public async Task Boundary_UnregisteredRepository_DoesNotAdoptClaims()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        await AssertRejected(boundary, fixture.Request(fixture.Sign(), repository: "unregistered"), "auth_project_mismatch");
    }

    [Theory]
    [InlineData("example")]
    [InlineData("unregistered")]
    public async Task Boundary_UnverifiedSignature_DoesNotRevealRegistration(string repository)
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        await AssertRejected(boundary, fixture.Request(fixture.Sign(keyId: "unknown"), repository: repository), "auth_key_unknown");
    }

    [Fact]
    public async Task Create_DuplicateUuid_RejectsConfiguration()
    {
        using Fixture fixture = new();
        ProviderAuthenticationOptions auth = fixture.Options.ProviderAuthentication!;
        BuckettieOptions invalid = fixture.Options with { ProviderAuthentication = auth with
        { Bindings = [auth.Bindings[0], auth.Bindings[0] with { Repository = "second", NormalizedRepository = "bitbucket.org/workspace/second" }] } };
        await Assert.ThrowsAsync<ProviderAuthenticationException>(() => ProviderAuthenticationBoundary.CreateAsync(invalid,
            new RepositoryAllowlist(invalid), NullLogger<ProviderAuthenticationBoundary>.Instance, TestContext.Current.CancellationToken, moyaiIntegration: true));
    }

    private static async Task AssertRejected(ProviderAuthenticationBoundary boundary, DefaultHttpContext context, string code, string? token = null)
    {
        bool called = false;
        await boundary.InvokeAsync(context, _ => { called = true; return Task.CompletedTask; });
        called.Should().BeFalse();
        context.Response.StatusCode.Should().Be(401);
        string body = Encoding.UTF8.GetString(((MemoryStream)context.Response.Body).ToArray());
        using JsonDocument response = JsonDocument.Parse(body);
        response.RootElement.GetProperty("error").GetProperty("data").GetProperty("code").GetString().Should().Be(code);
        if (token is not null) body.Should().NotContain(token);
    }

    [Fact]
    public async Task Boundary_DispatchHoldsRegistrationGate_AndReleasesOnFailure()
    {
        using Fixture fixture = new();
        using RepositoryMutationGate gate = new();
        ProviderAuthenticationBoundary boundary = await ProviderAuthenticationBoundary.CreateAsync(fixture.Options,
            new RepositoryAllowlist(fixture.Options), NullLogger<ProviderAuthenticationBoundary>.Instance,
            TestContext.Current.CancellationToken, gate, moyaiIntegration: true);
        await boundary.InvokeAsync(fixture.Request(fixture.Sign()), async _ =>
        {
            (await gate.TryEnterAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        });
        await AssertRejected(boundary, fixture.Request("invalid"), "auth_assertion_invalid");
        (await gate.TryEnterAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        gate.Release();
    }

    [Fact]
    public async Task Boundary_ChangedRegisteredIdentity_RejectsOldBinding()
    {
        using Fixture fixture = new();
        BuckettieOptions changed = fixture.Options with { Repositories = new Dictionary<string, RepositoryOptions>
        { ["example"] = fixture.Options.Repositories["example"] with { Slug = "replacement" } } };
        ProviderAuthenticationBoundary boundary = await ProviderAuthenticationBoundary.CreateAsync(changed,
            new RepositoryAllowlist(changed), NullLogger<ProviderAuthenticationBoundary>.Instance, TestContext.Current.CancellationToken, moyaiIntegration: true);
        await AssertRejected(boundary, fixture.Request(fixture.Sign()), "auth_project_mismatch");
    }

    [Fact]
    public async Task Boundary_NullTrustEntry_ReturnsSanitizedError()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        File.WriteAllText(fixture.Options.ProviderAuthentication!.TrustBundlePath, "[null]");
        await AssertRejected(boundary, fixture.Request(fixture.Sign()), "authentication_unavailable");
    }

    [Fact]
    public async Task Boundary_ReplayDatabaseUnavailable_RejectsBeforeDispatch()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        string path = fixture.Options.ProviderAuthentication!.ReplayDatabasePath;
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(path);
        Directory.CreateDirectory(path);
        await AssertRejected(boundary, fixture.Request(fixture.Sign()), "authentication_unavailable");
    }

    [Fact]
    public async Task Configuration_SaveAndReload_PreservesExplicitBinding()
    {
        using Fixture fixture = new();
        Buckettie.Infrastructure.Configuration.JsonBuckettieOptionsLoader loader = new();
        using MemoryStream json = new();
        await loader.SaveAsync(fixture.Options, json, TestContext.Current.CancellationToken);
        json.Position = 0;
        var result = await loader.LoadAsync(json, TestContext.Current.CancellationToken);
        result.IsValid.Should().BeTrue();
        result.Options!.ProviderAuthentication.Should().BeEquivalentTo(fixture.Options.ProviderAuthentication);
    }

    [Theory]
    [InlineData(149, true)]
    [InlineData(150, false)]
    [InlineData(151, false)]
    public async Task Boundary_ExpiryDuringPersistentReplayReservation_ChecksTimeBeforeDispatch(int elapsedSeconds, bool accepted)
    {
        using Fixture fixture = new();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        AdvancingClock clock = new(DateTimeOffset.FromUnixTimeSeconds(now));
        SqliteAssertionReplayCache persistent = new(new SqliteAssertionReplayCacheOptions(
            fixture.Options.ProviderAuthentication!.ReplayDatabasePath));
        await persistent.InitializeAsync(TestContext.Current.CancellationToken);
        AdvancingReplay replay = new(persistent, clock, TimeSpan.FromSeconds(elapsedSeconds));
        Es256AssertionValidator validator = new(new FileAssertionTrustStore(fixture.Options.ProviderAuthentication.TrustBundlePath),
            replay, new AssertionOptions("moyai:test"), ProviderToolPolicy.Capability, clock);
        ProviderAuthenticationBoundary boundary = new(fixture.Options, new RepositoryAllowlist(fixture.Options), validator,
            NullLogger<ProviderAuthenticationBoundary>.Instance);
        string jti = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        string token = fixture.Sign(new() { ["iat"] = now, ["nbf"] = now - 30, ["exp"] = now + 120, ["jti"] = jti });
        DefaultHttpContext context = fixture.Request(token);
        bool dispatched = false;
        await boundary.InvokeAsync(context, _ => { dispatched = true; return Task.CompletedTask; });
        dispatched.Should().Be(accepted);
        replay.Reserved.Should().BeTrue();
        if (!accepted)
        {
            context.Items.ContainsKey(typeof(AssertionPrincipal)).Should().BeFalse();
            context.Response.StatusCode.Should().Be(401);
            using JsonDocument result = JsonDocument.Parse(((MemoryStream)context.Response.Body).ToArray());
            result.RootElement.GetProperty("error").GetProperty("data").GetProperty("code").GetString()
                .Should().Be("auth_assertion_expired");
        }
        // The real SQLite reservation remains consumed even when the post-reservation check rejects.
        (await persistent.TryUseAsync("moyai:test", jti, DateTimeOffset.FromUnixTimeSeconds(now + 150),
            TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Theory]
    [InlineData("git", "valid", null)]
    [InlineData("git", "expired", "auth_assertion_expired")]
    [InlineData("git", "revoked", "auth_key_revoked")]
    [InlineData("git", "unavailable", "authentication_unavailable")]
    [InlineData("api", "valid", null)]
    [InlineData("api", "expired", "auth_assertion_expired")]
    [InlineData("api", "revoked", "auth_key_revoked")]
    [InlineData("api", "unavailable", "authentication_unavailable")]
    [InlineData("history", "valid", null)]
    [InlineData("history", "expired", "auth_assertion_expired")]
    [InlineData("history", "revoked", "auth_key_revoked")]
    [InlineData("history", "unavailable", "authentication_unavailable")]
    public async Task Execution_AfterWaiting_RevalidatesWithoutReservingReplay(string gateway, string scenario, string? error)
    {
        using Fixture fixture = new();
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        AdvancingClock clock = new(DateTimeOffset.FromUnixTimeSeconds(now));
        SqliteAssertionReplayCache persistent = new(new SqliteAssertionReplayCacheOptions(
            fixture.Options.ProviderAuthentication!.ReplayDatabasePath));
        await persistent.InitializeAsync(TestContext.Current.CancellationToken);
        CountingReplay replay = new(persistent);
        Es256AssertionValidator validator = new(new FileAssertionTrustStore(fixture.Options.ProviderAuthentication.TrustBundlePath),
            replay, new AssertionOptions("moyai:test"), ProviderToolPolicy.Capability, clock);
        ProviderAuthenticationBoundary boundary = new(fixture.Options, new RepositoryAllowlist(fixture.Options), validator,
            NullLogger<ProviderAuthenticationBoundary>.Instance);
        string tool = gateway switch { "api" => "bitbucket_branch_delete", "history" => "bitbucket_history_rewrite_preview", _ => "bitbucket_push" };
        string jti = Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        DefaultHttpContext context = fixture.Request(fixture.Sign(new()
        { ["iat"] = now, ["nbf"] = now - 30, ["exp"] = now + 120, ["jti"] = jti, ["scope"] = ProviderToolPolicy.Scopes(tool)! }), tool);
        IGitGateway git = Substitute.For<IGitGateway>();
        IBitbucketRepositoryGateway api = Substitute.For<IBitbucketRepositoryGateway>();
        GitHistoryRewriteRequest history = new("develop", "head", "test");
        git.PushAsync("example", Arg.Any<CancellationToken>()).Returns(GitGatewayResult.Success("push", "example", "develop"));
        git.PreviewHistoryRewriteAsync("example", history, Arg.Any<CancellationToken>())
            .Returns(GitGatewayResult.Success("preview", "example", "develop"));
        api.DeleteBranchAsync("example", "feature", Arg.Any<CancellationToken>()).Returns(BitbucketResult<bool>.Success(true));
        IBuckettieAuditLogger audit = Substitute.For<IBuckettieAuditLogger>();
        AuditedGitGateway guardedGit = new(git, audit, tool => ProviderAuthenticationBoundary.EnsureCurrentAsync(context, tool));
        AuditedBitbucketRepositoryGateway guardedApi = new(api, audit, tool => ProviderAuthenticationBoundary.EnsureCurrentAsync(context, tool));
        await boundary.InvokeAsync(context, async _ =>
        {
            // Model a queue/approval delay after initial authentication and before the actual gateway call.
            await Task.Yield();
            clock.Advance(TimeSpan.FromSeconds(scenario == "expired" ? 150 : 149));
            if (scenario == "revoked") fixture.WriteTrust(SigningKeyState.Revoked);
            if (scenario == "unavailable") File.WriteAllText(fixture.Options.ProviderAuthentication.TrustBundlePath, "[null]");
            Func<Task> execute = gateway switch
            {
                "api" => async () => { await guardedApi.DeleteBranchAsync("example", "feature", TestContext.Current.CancellationToken); },
                "history" => async () => { await guardedGit.PreviewHistoryRewriteAsync("example", history, TestContext.Current.CancellationToken); },
                _ => async () => { await guardedGit.PushAsync("example", TestContext.Current.CancellationToken); },
            };
            if (error is null) await execute();
            else (await Assert.ThrowsAsync<ModelContextProtocol.McpException>(execute)).Message.Should().Be(error);
        });
        (git.ReceivedCalls().Count() + api.ReceivedCalls().Count()).Should().Be(error is null ? 1 : 0);
        replay.Calls.Should().Be(1);
        (await persistent.TryUseAsync("moyai:test", jti, DateTimeOffset.FromUnixTimeSeconds(now + 150),
            TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    [Theory]
    [InlineData("list_projects", true)]
    [InlineData("bitbucket_repository_status", true)]
    [InlineData("bitbucket_repository_diff", true)]
    [InlineData("bitbucket_pr_diff", true)]
    [InlineData("buckettie_release_get", true)]
    [InlineData("bitbucket_history_rewrite_preview", true)]
    [InlineData("bitbucket_fetch", false)]
    [InlineData("bitbucket_pull", false)]
    [InlineData("bitbucket_push", false)]
    [InlineData("bitbucket_repository_commit", false)]
    [InlineData("buckettie_release_publish", false)]
    [InlineData("bitbucket_repository_register", false)]
    [InlineData("bitbucket_repository_update", false)]
    [InlineData("bitbucket_repository_unregister", false)]
    public void Policy_DirectRead_OnlyRepositoryReadAndProjectList(string tool, bool expected) =>
        ProviderToolPolicy.IsDirectRead(tool).Should().Be(expected);

    [Theory]
    [InlineData("bitbucket_repository_status")]
    [InlineData("list_projects")]
    public async Task Boundary_DirectLoopbackReadWithoutAssertion_Dispatches(string tool)
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        bool called = false;
        await boundary.InvokeAsync(fixture.DirectRequest(tool), _ => { called = true; return Task.CompletedTask; });
        called.Should().BeTrue();
    }

    [Fact]
    public async Task Boundary_DirectReadWithoutConfiguredAuthentication_Dispatches()
    {
        using Fixture fixture = new();
        BuckettieOptions unconfigured = fixture.Options with { ProviderAuthentication = null };
        ProviderAuthenticationBoundary boundary = await ProviderAuthenticationBoundary.CreateAsync(unconfigured,
            new RepositoryAllowlist(unconfigured), NullLogger<ProviderAuthenticationBoundary>.Instance, TestContext.Current.CancellationToken);
        bool called = false;
        await boundary.InvokeAsync(fixture.DirectRequest("bitbucket_repository_diff"), _ => { called = true; return Task.CompletedTask; });
        called.Should().BeTrue();
    }

    [Theory]
    [InlineData("bitbucket_push")]
    [InlineData("bitbucket_fetch")]
    [InlineData("bitbucket_pull")]
    [InlineData("bitbucket_repository_commit")]
    [InlineData("buckettie_release_publish")]
    public async Task Boundary_StandaloneWithoutMoyai_DispatchesChangeTools(string tool)
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateStandaloneAsync();
        bool called = false;
        await boundary.InvokeAsync(fixture.DirectRequest(tool), _ => { called = true; return Task.CompletedTask; });
        called.Should().BeTrue();
    }

    [Fact]
    public async Task Boundary_StandaloneFromNonLoopbackOrOtherPort_Rejects()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateStandaloneAsync();
        DefaultHttpContext remote = fixture.DirectRequest("bitbucket_push");
        remote.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        await AssertRejected(boundary, remote, "authentication_unavailable");
        DefaultHttpContext otherPort = fixture.DirectRequest("bitbucket_push");
        otherPort.Connection.LocalPort = fixture.Options.McpPort + 1;
        await AssertRejected(boundary, otherPort, "authentication_unavailable");
    }

    [Theory]
    [InlineData("bitbucket_repository_status")]
    [InlineData("bitbucket_push")]
    public async Task Boundary_StandaloneLoopbackWithMoyaiAssertion_IgnoresCredentialAndRemovesIt(string tool)
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateStandaloneAsync();
        DefaultHttpContext asserted = fixture.DirectRequest(tool);
        asserted.Request.Headers.Authorization = "Bearer moyai-assertion";
        bool called = false;
        await boundary.InvokeAsync(asserted, current =>
        {
            called = true;
            current.Request.Headers.Authorization.Should().BeEmpty();
            return Task.CompletedTask;
        });
        called.Should().BeTrue();
    }

    [Theory]
    [InlineData("server/discover")]
    [InlineData("resources/list")]
    [InlineData("resources/templates/list")]
    [InlineData("notifications/cancelled")]
    [InlineData("initialize")]
    public async Task Boundary_DiscoveryMethods_DispatchInBothModes(string method)
    {
        using Fixture fixture = new();
        foreach (ProviderAuthenticationBoundary boundary in new[] { await fixture.CreateAsync(), await fixture.CreateStandaloneAsync() })
        {
            DefaultHttpContext context = fixture.MethodRequest(method);
            bool called = false;
            await boundary.InvokeAsync(context, _ => { called = true; return Task.CompletedTask; });
            called.Should().BeTrue(method);
        }
    }

    [Fact]
    public async Task Boundary_UnknownMethod_ReturnsMethodNotAllowedCode()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        await AssertRejected(boundary, fixture.MethodRequest("sampling/createMessage"), "mcp_method_not_allowed");
    }

    [Fact]
    public async Task Boundary_GetSessionRequest_PassesThroughWithoutCredential()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        DefaultHttpContext context = new();
        context.Request.Method = "GET";
        context.Request.Path = "/mcp";
        context.Request.Headers.Authorization = "Bearer x";
        bool called = false;
        await boundary.InvokeAsync(context, current =>
        {
            called = true;
            current.Request.Headers.Authorization.Should().BeEmpty();
            return Task.CompletedTask;
        });
        called.Should().BeTrue();
    }

    [Fact]
    public async Task Execution_Standalone_AllowsGatewayOperationsWithoutPrincipal()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateStandaloneAsync();
        DefaultHttpContext context = fixture.DirectRequest("bitbucket_push");
        bool executed = false;
        await boundary.InvokeAsync(context, async _ =>
        {
            await ProviderAuthenticationBoundary.EnsureCurrentAsync(context, "bitbucket_push");
            executed = true;
        });
        executed.Should().BeTrue();
    }

    [Fact]
    public async Task Create_MoyaiModeWithoutAuthenticationConfiguration_FailsInsteadOfRunningStandalone()
    {
        using Fixture fixture = new();
        BuckettieOptions unconfigured = fixture.Options with { ProviderAuthentication = null };
        await Assert.ThrowsAsync<ProviderAuthenticationException>(() => ProviderAuthenticationBoundary.CreateAsync(unconfigured,
            new RepositoryAllowlist(unconfigured), NullLogger<ProviderAuthenticationBoundary>.Instance,
            TestContext.Current.CancellationToken, moyaiIntegration: true));
    }

    [Fact]
    public async Task Boundary_StandaloneStartWithConfiguredAuthentication_IgnoresMoyaiSettings()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await ProviderAuthenticationBoundary.CreateAsync(fixture.Options,
            new RepositoryAllowlist(fixture.Options), NullLogger<ProviderAuthenticationBoundary>.Instance,
            TestContext.Current.CancellationToken);
        bool called = false;
        await boundary.InvokeAsync(fixture.DirectRequest("bitbucket_push"), _ => { called = true; return Task.CompletedTask; });
        called.Should().BeTrue();
    }

    [Fact]
    public async Task Boundary_ConfiguredMoyaiIntegration_StillRequiresAssertionForChanges()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        await AssertRejected(boundary, fixture.DirectRequest("bitbucket_push"), "auth_assertion_missing");
    }

    [Theory]
    [InlineData("bitbucket_push", "auth_assertion_missing")]
    [InlineData("bitbucket_fetch", "auth_assertion_missing")]
    [InlineData("bitbucket_pull", "auth_assertion_missing")]
    public async Task Boundary_DirectLoopbackMutationWithoutAssertion_Rejects(string tool, string code)
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        await AssertRejected(boundary, fixture.DirectRequest(tool), code);
    }

    [Theory]
    [InlineData("bitbucket_repository_register")]
    [InlineData("bitbucket_repository_update")]
    [InlineData("bitbucket_repository_unregister")]
    public async Task Boundary_DirectLoopbackAdministration_DispatchesToDesktopApproval(string tool)
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        bool called = false;
        await boundary.InvokeAsync(fixture.DirectRequest(tool), _ => { called = true; return Task.CompletedTask; });
        called.Should().BeTrue();
    }

    [Theory]
    [InlineData("bitbucket_repository_register")]
    [InlineData("bitbucket_repository_unregister")]
    public async Task Boundary_AdministrationFromNonLoopbackOrWithAssertion_Rejects(string tool)
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        DefaultHttpContext remote = fixture.DirectRequest(tool);
        remote.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        await AssertRejected(boundary, remote, "auth_administrator_required");
        DefaultHttpContext asserted = fixture.DirectRequest(tool);
        asserted.Request.Headers.Authorization = "Bearer " + fixture.Sign();
        await AssertRejected(boundary, asserted, "auth_administrator_required");
        DefaultHttpContext otherPort = fixture.DirectRequest(tool);
        otherPort.Connection.LocalPort = fixture.Options.McpPort + 1;
        await AssertRejected(boundary, otherPort, "auth_administrator_required");
    }

    [Fact]
    public async Task Boundary_DirectReadFromNonLoopbackAddress_Rejects()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        DefaultHttpContext context = fixture.DirectRequest("bitbucket_repository_status");
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.10");
        await AssertRejected(boundary, context, "auth_assertion_missing");
    }

    [Fact]
    public async Task Boundary_DirectReadOnAnotherLocalPort_Rejects()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        DefaultHttpContext context = fixture.DirectRequest("list_projects");
        context.Connection.LocalPort = fixture.Options.McpPort + 1;
        await AssertRejected(boundary, context, "auth_administrator_required");
    }

    [Fact]
    public async Task Boundary_InvalidAssertionOnReadTool_DoesNotFallBackToDirectRead()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        DefaultHttpContext context = fixture.DirectRequest("bitbucket_repository_status");
        context.Request.Headers.Authorization = "Bearer old-service-token";
        await AssertRejected(boundary, context, "auth_assertion_invalid", "old-service-token");
    }

    [Fact]
    public async Task Execution_DirectRead_AllowsOnlyReadGatewayOperations()
    {
        using Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        DefaultHttpContext context = fixture.DirectRequest("bitbucket_repository_status");
        await boundary.InvokeAsync(context, async _ =>
        {
            await ProviderAuthenticationBoundary.EnsureCurrentAsync(context, "bitbucket_repository_status");
            (await Assert.ThrowsAsync<ModelContextProtocol.McpException>(
                () => ProviderAuthenticationBoundary.EnsureCurrentAsync(context, "bitbucket_push")))
                .Message.Should().Be("authentication_unavailable");
        });
    }

    [Fact]
    public async Task Execution_MissingAuthenticatedContext_FailsClosed()
    {
        (await Assert.ThrowsAsync<ModelContextProtocol.McpException>(() => ProviderAuthenticationBoundary.EnsureCurrentAsync(null, "bitbucket_push")))
            .Message.Should().Be("authentication_unavailable");
    }

    private sealed class CountingReplay(IAssertionReplayCache persistent) : IAssertionReplayCache
    {
        public int Calls { get; private set; }
        public Task<bool> TryUseAsync(string issuer, string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
        {
            Calls++;
            return persistent.TryUseAsync(issuer, jti, expiresAt, cancellationToken);
        }
    }

    private sealed class AdvancingClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan interval) => _now += interval;
    }

    private sealed class AdvancingReplay(IAssertionReplayCache persistent, AdvancingClock clock, TimeSpan interval) : IAssertionReplayCache
    {
        public bool Reserved { get; private set; }
        public async Task<bool> TryUseAsync(string issuer, string jti, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
        {
            Reserved = await persistent.TryUseAsync(issuer, jti, expiresAt, cancellationToken);
            clock.Advance(interval);
            return Reserved;
        }
    }

    internal sealed class Fixture : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "buckettie-auth-" + Guid.NewGuid().ToString("N"));
        private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        internal BuckettieOptions Options { get; }

        internal Fixture()
        {
            Directory.CreateDirectory(_directory);
            Options = new BuckettieOptions
            {
                AtlassianEmail = "test@example.com", BitbucketUsername = "testuser",
                Repositories = new Dictionary<string, RepositoryOptions> { ["example"] = new()
                {
                    Workspace = "workspace", Slug = "example", LocalRoot = _directory, Remote = "origin", DevelopBranch = "develop",
                    MainBranch = "main", DirectPushBranches = ["develop"], PullBranches = ["develop"], ProtectedBranches = ["main"],
                    TagTargetBranch = "main", TagPattern = "^v[0-9]+$",
                } },
                ProviderAuthentication = new()
                {
                    Issuer = "moyai:test", TrustBundlePath = Path.Combine(_directory, "trust.json"),
                    ReplayDatabasePath = Path.Combine(_directory, "replay.db"),
                    Bindings = [new() { Repository = "example", ProjectId = Guid.Parse("11111111-1111-4111-8111-111111111111"),
                        NormalizedRepository = "bitbucket.org/workspace/example" }],
                },
            };
            WriteTrust(SigningKeyState.Active);
        }

        internal Task<ProviderAuthenticationBoundary> CreateAsync() => ProviderAuthenticationBoundary.CreateAsync(Options,
            new RepositoryAllowlist(Options), NullLogger<ProviderAuthenticationBoundary>.Instance, TestContext.Current.CancellationToken, moyaiIntegration: true);

        /// <summary>Moyai連携を構成しない単体モードの境界です。</summary>
        internal Task<ProviderAuthenticationBoundary> CreateStandaloneAsync()
        {
            BuckettieOptions standalone = Options with { ProviderAuthentication = null };
            return ProviderAuthenticationBoundary.CreateAsync(standalone, new RepositoryAllowlist(standalone),
                NullLogger<ProviderAuthenticationBoundary>.Instance, TestContext.Current.CancellationToken);
        }

        internal void WriteTrust(SigningKeyState state)
        {
            ECParameters key = _key.ExportParameters(false);
            AssertionTrustKey trust = new("moyai:test", "key-1", "ES256", Encode(key.Q.X!), Encode(key.Q.Y!),
                DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1), state);
            File.WriteAllText(Options.ProviderAuthentication!.TrustBundlePath, JsonSerializer.Serialize(new[] { trust },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
        }

        internal string Sign(Dictionary<string, object>? changes = null, string algorithm = "ES256", string keyId = "key-1")
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Dictionary<string, object> claims = new()
            {
                ["iss"] = "moyai:test", ["sub"] = "moyai:test", ["aud"] = "buckettie", ["prv"] = "buckettie",
                ["iat"] = now, ["nbf"] = now - 30, ["exp"] = now + 120, ["jti"] = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)),
                ["project"] = Options.ProviderAuthentication!.Bindings[0].ProjectId.ToString("D"), ["repository"] = "bitbucket.org/workspace/example",
                ["scope"] = new[] { "repository.push" }, ["protocol_version"] = "1", ["operation_id"] = "operation-1",
            };
            if (changes is not null) foreach (var change in changes) claims[change.Key] = change.Value;
            string header = Encode(JsonSerializer.SerializeToUtf8Bytes(new { alg = algorithm, typ = "JWT", kid = keyId }));
            string payload = Encode(JsonSerializer.SerializeToUtf8Bytes(claims));
            string input = header + "." + payload;
            return input + "." + Encode(_key.SignData(Encoding.ASCII.GetBytes(input), HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
        }

        internal DefaultHttpContext Request(string? token, string tool = "bitbucket_push", string repository = "example")
        {
            DefaultHttpContext context = new();
            context.Request.Method = "POST";
            context.Request.Path = "/mcp";
            context.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new
            { jsonrpc = "2.0", id = 1, method = "tools/call", @params = new { name = tool, arguments = new { repository } } }));
            context.Response.Body = new MemoryStream();
            if (token is not null) context.Request.Headers.Authorization = "Bearer " + token;
            context.Request.Headers["X-Moyai-Operation-Id"] = "operation-1";
            return context;
        }

        /// <summary>tools/call以外のMCP要求です。</summary>
        internal DefaultHttpContext MethodRequest(string method)
        {
            DefaultHttpContext context = new();
            context.Request.Method = "POST";
            context.Request.Path = "/mcp";
            context.Request.Body = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(new
            { jsonrpc = "2.0", id = 1, method, @params = new { } }));
            context.Response.Body = new MemoryStream();
            return context;
        }

        /// <summary>Assertionを付けないloopback直接接続の要求です。</summary>
        internal DefaultHttpContext DirectRequest(string tool)
        {
            DefaultHttpContext context = Request(null, tool);
            context.Request.Headers.Remove("X-Moyai-Operation-Id");
            context.Connection.RemoteIpAddress = IPAddress.Loopback;
            context.Connection.LocalPort = Options.McpPort;
            return context;
        }

        private static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        public void Dispose()
        {
            _key.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            Directory.Delete(_directory, true);
        }
    }
}
