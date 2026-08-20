using System.Security.Principal;
using Buckettie.Application.Interactive;
using Microsoft.Win32.SafeHandles;

namespace Buckettie.Infrastructure.Interactive;

/// <summary>
/// WTS Session TokenとNamed Pipeを組み合わせ、対話Desktopで承認Dialogを起動・待受します。
/// </summary>
public sealed class WindowsInteractiveApprovalPrompt : IInteractiveApprovalPrompt
{
    private readonly ISessionTokenProvider _sessionTokenProvider;
    private readonly IApprovalProcessLauncher _processLauncher;
    private readonly IApprovalPipeTransport _pipeTransport;
    private readonly string _approvalPromptExecutable;
    private readonly string _language;

    /// <summary>実運用向けのWindows実装で初期化します。</summary>
    public WindowsInteractiveApprovalPrompt(string approvalPromptExecutable, string language = "auto")
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException();
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(approvalPromptExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        _sessionTokenProvider = new WtsSessionTokenProvider();
        _processLauncher = new TaskSchedulerProcessLauncher();
        _pipeTransport = new NamedPipeApprovalTransport();
        _approvalPromptExecutable = approvalPromptExecutable;
        _language = language;
    }

    /// <summary>テスト用にSeamをすべて注入して初期化します。</summary>
    internal WindowsInteractiveApprovalPrompt(
        ISessionTokenProvider sessionTokenProvider,
        IApprovalProcessLauncher processLauncher,
        IApprovalPipeTransport pipeTransport,
        string approvalPromptExecutable,
        string language = "auto")
    {
        ArgumentNullException.ThrowIfNull(sessionTokenProvider);
        ArgumentNullException.ThrowIfNull(processLauncher);
        ArgumentNullException.ThrowIfNull(pipeTransport);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalPromptExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        _sessionTokenProvider = sessionTokenProvider;
        _processLauncher = processLauncher;
        _pipeTransport = pipeTransport;
        _approvalPromptExecutable = approvalPromptExecutable;
        _language = language;
    }

    /// <inheritdoc />
    public async Task<ApprovalPromptOutcome> RequestApprovalAsync(
        ApprovalPromptRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        bool hasToken = _sessionTokenProvider.TryGetActiveSessionToken(
            out SafeAccessTokenHandle? token, out SecurityIdentifier? userSid);
        if (!hasToken || token is null || userSid is null)
        {
            return ApprovalPromptOutcome.Failure(ApprovalOutcome.NoInteractiveSession);
        }

        using (token)
        {
            await using IApprovalPipeServer server = _pipeTransport.CreateServer(userSid);

            if (!_processLauncher.TryLaunch(token, _approvalPromptExecutable, server.PipeName))
            {
                return ApprovalPromptOutcome.Failure(ApprovalOutcome.LaunchFailed);
            }

            if (!await server.WaitForClientAsync(timeout, cancellationToken).ConfigureAwait(false))
            {
                return ApprovalPromptOutcome.Failure(ApprovalOutcome.TimedOut);
            }

            ApprovalOutcome outcome = await server.ExchangeAsync(
                    request with { Language = _language }, timeout, cancellationToken)
                .ConfigureAwait(false);
            return outcome switch
            {
                ApprovalOutcome.Approved => ApprovalPromptOutcome.Approved(),
                ApprovalOutcome.Denied => ApprovalPromptOutcome.Denied(),
                _ => ApprovalPromptOutcome.Failure(outcome),
            };
        }
    }
}
