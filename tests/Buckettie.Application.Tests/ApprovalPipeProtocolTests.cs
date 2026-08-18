using Buckettie.Application.Interactive;
using FluentAssertions;
using Xunit;

namespace Buckettie.Application.Tests;

public sealed class ApprovalPipeProtocolTests
{
    [Fact]
    public async Task WriteFrameAsync_ThenReadFrameAsync_RoundTripsTheRequest()
    {
        ApprovalPromptRequest request = new(
            "new-repo", "example-workspace", "buckettie", "C:\\Repositories\\Buckettie",
            "https://bitbucket.org/example-workspace/buckettie.git");
        await using MemoryStream stream = new();

        await ApprovalPipeProtocol.WriteFrameAsync(stream, request, TestContext.Current.CancellationToken);
        stream.Position = 0;
        ApprovalPromptRequest? actual = await ApprovalPipeProtocol.ReadFrameAsync<ApprovalPromptRequest>(
            stream, TestContext.Current.CancellationToken);

        actual.Should().Be(request);
    }

    [Fact]
    public async Task WriteFrameAsync_ThenReadFrameAsync_RoundTripsTheResponse()
    {
        ApprovalPromptResponse response = new(true);
        await using MemoryStream stream = new();

        await ApprovalPipeProtocol.WriteFrameAsync(stream, response, TestContext.Current.CancellationToken);
        stream.Position = 0;
        ApprovalPromptResponse? actual = await ApprovalPipeProtocol.ReadFrameAsync<ApprovalPromptResponse>(
            stream, TestContext.Current.CancellationToken);

        actual.Should().Be(response);
    }

    [Fact]
    public async Task ReadFrameAsync_WhenStreamEndsBeforeHeaderIsComplete_ReturnsNull()
    {
        await using MemoryStream stream = new(new byte[] { 0x01, 0x00 });

        ApprovalPromptResponse? actual = await ApprovalPipeProtocol.ReadFrameAsync<ApprovalPromptResponse>(
            stream, TestContext.Current.CancellationToken);

        actual.Should().BeNull();
    }

    [Fact]
    public async Task ReadFrameAsync_WhenDeclaredLengthExceedsMaximum_ReturnsNull()
    {
        await using MemoryStream stream = new();
        await stream.WriteAsync(
            BitConverter.GetBytes(ApprovalPipeProtocol.MaxPayloadBytes + 1),
            TestContext.Current.CancellationToken);
        stream.Position = 0;

        ApprovalPromptResponse? actual = await ApprovalPipeProtocol.ReadFrameAsync<ApprovalPromptResponse>(
            stream, TestContext.Current.CancellationToken);

        actual.Should().BeNull();
    }

    [Fact]
    public async Task ReadFrameAsync_WhenDeclaredLengthIsNegative_ReturnsNull()
    {
        await using MemoryStream stream = new();
        await stream.WriteAsync(BitConverter.GetBytes(-1), TestContext.Current.CancellationToken);
        stream.Position = 0;

        ApprovalPromptResponse? actual = await ApprovalPipeProtocol.ReadFrameAsync<ApprovalPromptResponse>(
            stream, TestContext.Current.CancellationToken);

        actual.Should().BeNull();
    }

    [Fact]
    public async Task ReadFrameAsync_WhenPayloadIsTruncated_ReturnsNull()
    {
        await using MemoryStream stream = new();
        await stream.WriteAsync(BitConverter.GetBytes(100), TestContext.Current.CancellationToken);
        await stream.WriteAsync(new byte[] { 0x7B, 0x22 }, TestContext.Current.CancellationToken);
        stream.Position = 0;

        ApprovalPromptResponse? actual = await ApprovalPipeProtocol.ReadFrameAsync<ApprovalPromptResponse>(
            stream, TestContext.Current.CancellationToken);

        actual.Should().BeNull();
    }

    [Fact]
    public async Task ReadFrameAsync_WhenPayloadIsMalformedJson_ReturnsNull()
    {
        await using MemoryStream stream = new();
        byte[] payload = "not-json"u8.ToArray();
        await stream.WriteAsync(BitConverter.GetBytes(payload.Length), TestContext.Current.CancellationToken);
        await stream.WriteAsync(payload, TestContext.Current.CancellationToken);
        stream.Position = 0;

        ApprovalPromptResponse? actual = await ApprovalPipeProtocol.ReadFrameAsync<ApprovalPromptResponse>(
            stream, TestContext.Current.CancellationToken);

        actual.Should().BeNull();
    }
}
