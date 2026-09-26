using Buckettie.Application.Configuration;
using Buckettie.Application.Interactive;
using Buckettie.Application.Repositories;

namespace Buckettie.Server;

/// <summary>Repository登録解除要求を1つの流れとして実行する境界です。</summary>
public interface IRepositoryUnregistrationService
{
    /// <summary>Repositoryの登録解除を試みます。</summary>
    Task<RepositoryUnregistrationOutcome> UnregisterAsync(string repositoryId, CancellationToken cancellationToken);
}

/// <summary>
/// Repository登録解除を実行します。登録・修正と同じ対話Desktopでの人間承認を要求します。
/// loopback直接接続から管理者証明書なしで呼び出せるため、この承認が操作の認可になります。
/// </summary>
internal sealed class RepositoryUnregistrationService : IRepositoryUnregistrationService
{
    private static readonly TimeSpan ApprovalTimeout = TimeSpan.FromSeconds(120);

    private readonly RepositoryAllowlist _allowlist;
    private readonly IRepositoryStore _repositoryStore;
    private readonly IInteractiveApprovalPrompt _approvalPrompt;
    private readonly RepositoryMutationGate _gate;

    /// <summary>登録解除Serviceを初期化します。</summary>
    public RepositoryUnregistrationService(
        RepositoryAllowlist allowlist,
        IRepositoryStore repositoryStore,
        IInteractiveApprovalPrompt approvalPrompt,
        RepositoryMutationGate gate)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        ArgumentNullException.ThrowIfNull(repositoryStore);
        ArgumentNullException.ThrowIfNull(approvalPrompt);
        ArgumentNullException.ThrowIfNull(gate);
        _allowlist = allowlist;
        _repositoryStore = repositoryStore;
        _approvalPrompt = approvalPrompt;
        _gate = gate;
    }

    /// <inheritdoc />
    public async Task<RepositoryUnregistrationOutcome> UnregisterAsync(
        string repositoryId, CancellationToken cancellationToken)
    {
        if (!RepositoryId.IsLookupValid(repositoryId))
        {
            return RepositoryUnregistrationOutcome.Failure(
                BuckettieToolResultMapper.RegistrationValidationError(RepositoryValidationError.RepositoryIdInvalid));
        }

        if (!await _gate.TryEnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return RepositoryUnregistrationOutcome.Failure(BuckettieToolResultMapper.RegistrationInProgressError());
        }

        try
        {
            if (!_allowlist.TryGet(repositoryId, out RepositoryOptions? existing) || existing is null)
            {
                return RepositoryUnregistrationOutcome.Failure(
                    BuckettieToolResultMapper.RegistrationValidationError(
                        RepositoryValidationError.RepositoryNotRegistered));
            }

            ApprovalPromptRequest promptRequest = new(
                repositoryId, existing.Workspace, existing.Slug, existing.LocalRoot, existing.Remote,
                Operation: ApprovalOperation.Unregister);
            ApprovalPromptOutcome approval = await _approvalPrompt
                .RequestApprovalAsync(promptRequest, ApprovalTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (approval.Outcome != ApprovalOutcome.Approved)
            {
                return RepositoryUnregistrationOutcome.Failure(
                    BuckettieToolResultMapper.RegistrationApprovalError(approval.Outcome));
            }

            bool deleted = await _repositoryStore.DeleteAsync(repositoryId, cancellationToken)
                .ConfigureAwait(false);
            if (!deleted)
            {
                return RepositoryUnregistrationOutcome.Failure(BuckettieToolResultMapper.RegistrationWriteFailedError());
            }

            _allowlist.Unregister(repositoryId);
            return RepositoryUnregistrationOutcome.Success(repositoryId);
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>Repository登録解除要求の結果です。</summary>
public sealed record RepositoryUnregistrationOutcome(bool IsSuccess, string? RepositoryId, BuckettieToolError? Error)
{
    /// <summary>成功結果を生成します。</summary>
    public static RepositoryUnregistrationOutcome Success(string repositoryId) => new(true, repositoryId, null);

    /// <summary>失敗結果を生成します。</summary>
    public static RepositoryUnregistrationOutcome Failure(BuckettieToolError error) => new(false, null, error);
}
