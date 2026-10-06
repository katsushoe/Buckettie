using System.Text.Json;
using Buckettie.Application.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moyai.ProviderAuthentication;
using Xunit;

namespace Buckettie.Server.Tests;

public sealed class RepositoryContentionTests
{
    [Fact]
    public async Task Boundary_DirectReadAndAssertionOverlap_WaitsThenDispatches()
    {
        using ProviderAuthenticationTests.Fixture fixture = new();
        ProviderAuthenticationBoundary boundary = await fixture.CreateAsync();
        TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Task first = boundary.InvokeAsync(fixture.DirectRequest("bitbucket_repository_status"), async _ =>
        {
            entered.SetResult();
            await release.Task;
        });
        await entered.Task;
        int dispatched = 0;
        DefaultHttpContext second = fixture.Request(fixture.Sign(new() { ["scope"] = new[] { "repository.read" } }),
            "bitbucket_repository_status");
        Task waiting = boundary.InvokeAsync(second, _ => { dispatched++; return Task.CompletedTask; });
        try
        {
            waiting.IsCompleted.Should().BeFalse();
            dispatched.Should().Be(0);
        }
        finally { release.TrySetResult(); }
        await Task.WhenAll(first, waiting);
        dispatched.Should().Be(1);
        second.Response.StatusCode.Should().Be(200);
        second.Items[typeof(AssertionPrincipal)].Should().NotBeNull();
    }

    [Fact]
    public async Task Boundary_GateTimeout_ReturnsRetryable503WithoutConsumingAssertion()
    {
        using ProviderAuthenticationTests.Fixture fixture = new();
        using RepositoryMutationGate gate = new();
        ProviderAuthenticationBoundary boundary = await CreateAsync(fixture, gate);
        (await gate.TryEnterAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        string assertion = fixture.Sign();
        DefaultHttpContext request = fixture.Request(assertion);
        try
        {
            await boundary.InvokeAsync(request, _ => throw new InvalidOperationException("Must not dispatch"));
            request.Response.StatusCode.Should().Be(503);
            request.Response.Headers.RetryAfter.ToString().Should().Be("1");
            request.Response.Body.Position = 0;
            using JsonDocument response = await JsonDocument.ParseAsync(request.Response.Body,
                cancellationToken: TestContext.Current.CancellationToken);
            response.RootElement.GetProperty("id").GetInt32().Should().Be(1);
            JsonElement error = response.RootElement.GetProperty("error");
            error.GetProperty("code").GetInt32().Should().Be(-32002);
            error.GetProperty("data").GetProperty("code").GetString().Should().Be("repository_busy");
            error.GetProperty("data").GetProperty("retryable").GetBoolean().Should().BeTrue();
            error.GetProperty("data").GetProperty("outcome").GetString().Should().Be("not_executed");
            response.RootElement.GetRawText().Should().NotContain(assertion);
            (await gate.TryEnterAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        }
        finally { gate.Release(); }
        bool called = false;
        await boundary.InvokeAsync(fixture.Request(assertion), _ => { called = true; return Task.CompletedTask; });
        called.Should().BeTrue();
    }

    [Fact]
    public async Task Boundary_CancelledWait_DoesNotReleaseOwnersGate()
    {
        using ProviderAuthenticationTests.Fixture fixture = new();
        using RepositoryMutationGate gate = new();
        ProviderAuthenticationBoundary boundary = await CreateAsync(fixture, gate);
        (await gate.TryEnterAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        using CancellationTokenSource cancelled = new();
        DefaultHttpContext request = fixture.Request(fixture.Sign());
        request.RequestAborted = cancelled.Token;
        Task waiting = boundary.InvokeAsync(request, _ => throw new InvalidOperationException("Must not dispatch"));
        cancelled.Cancel();
        try
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            (await gate.TryEnterAsync(TestContext.Current.CancellationToken)).Should().BeFalse();
        }
        finally { gate.Release(); }
        (await gate.TryEnterAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        gate.Release();
    }

    [Fact]
    public async Task Boundary_KeyRevokedWhileWaiting_RejectsAndReleasesGate()
    {
        using ProviderAuthenticationTests.Fixture fixture = new();
        using RepositoryMutationGate gate = new();
        ProviderAuthenticationBoundary boundary = await CreateAsync(fixture, gate);
        (await gate.TryEnterAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        DefaultHttpContext request = fixture.Request(fixture.Sign());
        Task waiting = boundary.InvokeAsync(request, _ => throw new InvalidOperationException("Must not dispatch"));
        try
        {
            waiting.IsCompleted.Should().BeFalse();
            fixture.WriteTrust(SigningKeyState.Revoked);
        }
        finally { gate.Release(); }
        await waiting;
        request.Response.StatusCode.Should().Be(401);
        request.Response.Body.Position = 0;
        using JsonDocument response = await JsonDocument.ParseAsync(request.Response.Body,
            cancellationToken: TestContext.Current.CancellationToken);
        response.RootElement.GetProperty("error").GetProperty("data").GetProperty("code")
            .GetString().Should().Be("auth_key_revoked");
        (await gate.TryEnterAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        gate.Release();
    }

    private static Task<ProviderAuthenticationBoundary> CreateAsync(ProviderAuthenticationTests.Fixture fixture,
        RepositoryMutationGate gate) => ProviderAuthenticationBoundary.CreateAsync(fixture.Options,
            new RepositoryAllowlist(fixture.Options), NullLogger<ProviderAuthenticationBoundary>.Instance,
            TestContext.Current.CancellationToken, gate, moyaiIntegration: true);
}
