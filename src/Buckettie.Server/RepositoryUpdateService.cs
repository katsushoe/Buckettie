using Buckettie.Application.Configuration;
using Buckettie.Application.Interactive;
using Buckettie.Application.Repositories;

namespace Buckettie.Server;

/// <summary>更新後に適用するBranch Policyです。RepositoryのWorkspace/Slug/LocalRoot等の識別子は含みません。</summary>
public sealed record RepositoryUpdateRequest(
    HashSet<string> DirectPushBranches,
    HashSet<string> PullBranches,
    HashSet<string> ProtectedBranches,
    string TagTargetBranch,
    string TagPattern,
    bool RequireCleanWorkingTree);

/// <summary>Repository修正要求を1つの流れとして実行する境界です。</summary>
public interface IRepositoryUpdateService
{
    /// <summary>Repositoryの登録済みBranch Policyの修正を試みます。</summary>
    Task<RepositoryUpdateOutcome> UpdateAsync(
        string repositoryId, RepositoryUpdateRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Repositoryの登録済みBranch Policyを修正します。より寛容な権限を許可し得るため、
/// 登録と同じ対話Desktopでの人間承認を要求します。Workspace/Slug/LocalRoot/Remote/
/// DevelopBranch/MainBranchは登録時にGit Remoteから検証済みの値であり、この操作では
/// 変更しません（変更するには登録解除して再登録します）。
/// </summary>
internal sealed class RepositoryUpdateService : IRepositoryUpdateService
{
    private static readonly TimeSpan ApprovalTimeout = TimeSpan.FromSeconds(120);

    private readonly RepositoryAllowlist _allowlist;
    private readonly IRepositoryStore _repositoryStore;
    private readonly IInteractiveApprovalPrompt _approvalPrompt;
    private readonly RepositoryMutationGate _gate;

    /// <summary>修正Serviceを初期化します。</summary>
    public RepositoryUpdateService(
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
    public async Task<RepositoryUpdateOutcome> UpdateAsync(
        string repositoryId, RepositoryUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!RepositoryId.IsLookupValid(repositoryId))
        {
            return RepositoryUpdateOutcome.Failure(
                BuckettieToolResultMapper.RegistrationValidationError(RepositoryValidationError.RepositoryIdInvalid));
        }

        if (!TagPatternValidator.IsValid(request.TagPattern))
        {
            return RepositoryUpdateOutcome.Failure(
                BuckettieToolResultMapper.RegistrationValidationError(RepositoryValidationError.TagPatternInvalid));
        }

        if (!await _gate.TryEnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return RepositoryUpdateOutcome.Failure(BuckettieToolResultMapper.RegistrationInProgressError());
        }

        try
        {
            if (!_allowlist.TryGet(repositoryId, out RepositoryOptions? existing) || existing is null)
            {
                return RepositoryUpdateOutcome.Failure(
                    BuckettieToolResultMapper.RegistrationValidationError(
                        RepositoryValidationError.RepositoryNotRegistered));
            }

            ApprovalPromptRequest promptRequest = new(
                repositoryId, existing.Workspace, existing.Slug, existing.LocalRoot, existing.Remote);
            ApprovalPromptOutcome approval = await _approvalPrompt
                .RequestApprovalAsync(promptRequest, ApprovalTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (approval.Outcome != ApprovalOutcome.Approved)
            {
                return RepositoryUpdateOutcome.Failure(
                    BuckettieToolResultMapper.RegistrationApprovalError(approval.Outcome));
            }

            RepositoryOptions updated = existing with
            {
                DirectPushBranches = request.DirectPushBranches,
                PullBranches = request.PullBranches,
                ProtectedBranches = request.ProtectedBranches,
                TagTargetBranch = request.TagTargetBranch,
                TagPattern = request.TagPattern,
                RequireCleanWorkingTree = request.RequireCleanWorkingTree,
            };

            bool written = await _repositoryStore.UpdateAsync(repositoryId, updated, cancellationToken)
                .ConfigureAwait(false);
            if (!written)
            {
                return RepositoryUpdateOutcome.Failure(BuckettieToolResultMapper.RegistrationWriteFailedError());
            }

            _allowlist.Update(repositoryId, updated);
            return RepositoryUpdateOutcome.Success(repositoryId);
        }
        finally
        {
            _gate.Release();
        }
    }
}

/// <summary>Repository修正要求の結果です。</summary>
public sealed record RepositoryUpdateOutcome(bool IsSuccess, string? RepositoryId, BuckettieToolError? Error)
{
    /// <summary>成功結果を生成します。</summary>
    public static RepositoryUpdateOutcome Success(string repositoryId) => new(true, repositoryId, null);

    /// <summary>失敗結果を生成します。</summary>
    public static RepositoryUpdateOutcome Failure(BuckettieToolError error) => new(false, null, error);
}
