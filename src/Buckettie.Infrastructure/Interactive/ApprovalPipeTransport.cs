using System.IO.Pipes;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Buckettie.Application.Interactive;

namespace Buckettie.Infrastructure.Interactive;

/// <summary>
/// 承認要求1回分のNamed Pipe Sessionです。
/// </summary>
internal interface IApprovalPipeServer : IAsyncDisposable
{
    /// <summary>承認Prompt processへ渡すPipe名です。</summary>
    string PipeName { get; }

    /// <summary>承認Prompt processの接続を待ちます。</summary>
    Task<bool> WaitForClientAsync(TimeSpan timeout, CancellationToken cancellationToken);

    /// <summary>要求を送信し、応答を待ちます。</summary>
    Task<ApprovalOutcome> ExchangeAsync(
        ApprovalPromptRequest request,
        TimeSpan responseTimeout,
        CancellationToken cancellationToken);
}

/// <summary>
/// Named Pipe Serverを生成する仕組みです。
/// </summary>
internal interface IApprovalPipeTransport
{
    /// <summary>指定Userだけが接続できるPipe Serverを作成します。</summary>
    IApprovalPipeServer CreateServer(SecurityIdentifier allowedUser);
}

/// <summary>
/// GUID付きの使い捨てPipe名でNamed Pipe Serverを作成します。ACLは対象Userのみへ限定します。
/// </summary>
[SupportedOSPlatform("windows")]
internal sealed class NamedPipeApprovalTransport : IApprovalPipeTransport
{
    /// <inheritdoc />
    public IApprovalPipeServer CreateServer(SecurityIdentifier allowedUser)
    {
        ArgumentNullException.ThrowIfNull(allowedUser);
        string pipeName = $"Buckettie-Approval-{Guid.NewGuid():N}";

        PipeSecurity security = new();
        security.AddAccessRule(new PipeAccessRule(allowedUser, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        NamedPipeServerStream stream = NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.InOut,
            1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 4096,
            outBufferSize: 4096,
            pipeSecurity: security);
        return new NamedPipeApprovalServer(pipeName, stream);
    }
}

/// <summary>1回の承認要求だけを扱うNamed Pipe Serverです。</summary>
internal sealed class NamedPipeApprovalServer(string pipeName, Stream stream) : IApprovalPipeServer
{
    /// <inheritdoc />
    public string PipeName { get; } = pipeName;

    /// <inheritdoc />
    public async Task<bool> WaitForClientAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        if (stream is not NamedPipeServerStream pipe)
        {
            return true;
        }

        using CancellationTokenSource timeoutSource = new(timeout);
        using CancellationTokenSource linked =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
        try
        {
            await pipe.WaitForConnectionAsync(linked.Token).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<ApprovalOutcome> ExchangeAsync(
        ApprovalPromptRequest request,
        TimeSpan responseTimeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        try
        {
            await ApprovalPipeProtocol.WriteFrameAsync(stream, request, cancellationToken).ConfigureAwait(false);

            using CancellationTokenSource timeoutSource = new(responseTimeout);
            using CancellationTokenSource linked =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutSource.Token);
            ApprovalPromptResponse? response;
            try
            {
                response = await ApprovalPipeProtocol.ReadFrameAsync<ApprovalPromptResponse>(stream, linked.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return ApprovalOutcome.TimedOut;
            }

            if (response is null)
            {
                return ApprovalOutcome.ProtocolError;
            }

            return response.Approved ? ApprovalOutcome.Approved : ApprovalOutcome.Denied;
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException)
        {
            return ApprovalOutcome.ProtocolError;
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync() => await stream.DisposeAsync().ConfigureAwait(false);
}
