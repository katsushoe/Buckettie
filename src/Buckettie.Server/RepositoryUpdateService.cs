using Buckettie.Application.Configuration;
using Buckettie.Application.Git;
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
    bool RequireCleanWorkingTree,
    HashSet<string>? HistoryRewriteBranches = null,
    string? CommitAuthorName = null,
    string? CommitAuthorEmail = null,
    Guid? MoyaiProjectId = null,
    bool RemoveMoyaiProjectId = false);

/// <summary>Repository Settings部分更新です。Fieldsに含む項目だけが更新対象です。</summary>
public sealed record RepositorySettingsChanges(
    IReadOnlySet<string> Fields,
    HashSet<string>? DirectPushBranches = null,
    HashSet<string>? ProtectedBranches = null,
    HashSet<string>? PullBranches = null,
    string? TagPattern = null,
    string? TagTargetBranch = null,
    bool? RequireCleanWorkingTree = null,
    string? CommitAuthorName = null,
    string? CommitAuthorEmail = null);

/// <summary>Repository修正要求を1つの流れとして実行する境界です。</summary>
public interface IRepositoryUpdateService
{
    /// <summary>Repositoryの登録済みBranch Policyの修正を試みます。</summary>
    Task<RepositoryUpdateOutcome> UpdateAsync(
        string repositoryId, RepositoryUpdateRequest request, CancellationToken cancellationToken);

    /// <summary>最新の登録値をロック下で読み、指定項目だけ部分更新します。</summary>
    Task<RepositoryUpdateOutcome> UpdateSettingsAsync(
        string repositoryId, RepositorySettingsChanges changes, CancellationToken cancellationToken,
        Func<CancellationToken, Task>? revalidate = null, bool operationGateHeld = false);
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
    private readonly ProviderAuthenticationOptions? _authentication;

    /// <summary>修正Serviceを初期化します。</summary>
    /// <param name="authentication">設定ファイルのMoyai Binding。Project IDの重複検出に使います。</param>
    public RepositoryUpdateService(
        RepositoryAllowlist allowlist,
        IRepositoryStore repositoryStore,
        IInteractiveApprovalPrompt approvalPrompt,
        RepositoryMutationGate gate,
        ProviderAuthenticationOptions? authentication = null)
    {
        ArgumentNullException.ThrowIfNull(allowlist);
        ArgumentNullException.ThrowIfNull(repositoryStore);
        ArgumentNullException.ThrowIfNull(approvalPrompt);
        ArgumentNullException.ThrowIfNull(gate);
        _allowlist = allowlist;
        _repositoryStore = repositoryStore;
        _approvalPrompt = approvalPrompt;
        _gate = gate;
        _authentication = authentication;
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

        // The author is changed only as a pair so a half-updated identity can never be stored.
        bool authorSpecified = request.CommitAuthorName is not null || request.CommitAuthorEmail is not null;
        if (authorSpecified && !GitCommitAuthor.IsValid(request.CommitAuthorName, request.CommitAuthorEmail))
        {
            return RepositoryUpdateOutcome.Failure(
                BuckettieToolResultMapper.RegistrationValidationError(RepositoryValidationError.CommitAuthorInvalid));
        }

        if (request.MoyaiProjectId == Guid.Empty || (request.MoyaiProjectId is not null && request.RemoveMoyaiProjectId))
        {
            return RepositoryUpdateOutcome.Failure(
                BuckettieToolResultMapper.RegistrationValidationError(RepositoryValidationError.MoyaiProjectIdInvalid));
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

            if (request.MoyaiProjectId is { } projectId
                && MoyaiBindings.Conflicts(projectId, repositoryId, _allowlist, _authentication))
            {
                return RepositoryUpdateOutcome.Failure(
                    BuckettieToolResultMapper.RegistrationValidationError(RepositoryValidationError.MoyaiProjectIdConflict));
            }

            // The dialog shows the binding change so a human approves exactly which Moyai Project gains access.
            string? bindingChange = request.MoyaiProjectId?.ToString("D")
                ?? (request.RemoveMoyaiProjectId ? $"{existing.MoyaiProjectId?.ToString("D") ?? "-"} -> (remove)" : null);
            ApprovalPromptRequest promptRequest = new(
                repositoryId, existing.Workspace, existing.Slug, existing.LocalRoot, existing.Remote ?? BitbucketRemoteUrlValidator.RepositoryUrl(existing.Workspace, existing.Slug),
                Operation: ApprovalOperation.Update, MoyaiProjectId: bindingChange);
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
                HistoryRewriteBranches = request.HistoryRewriteBranches ?? existing.HistoryRewriteBranches,
                CommitAuthorName = authorSpecified ? request.CommitAuthorName : existing.CommitAuthorName,
                CommitAuthorEmail = authorSpecified ? request.CommitAuthorEmail : existing.CommitAuthorEmail,
                MoyaiProjectId = request.RemoveMoyaiProjectId ? null : request.MoyaiProjectId ?? existing.MoyaiProjectId,
            };

            bool written = await _repositoryStore.UpdateAsync(repositoryId, updated, cancellationToken)
                .ConfigureAwait(false);
            if (!written)
            {
                return RepositoryUpdateOutcome.Failure(BuckettieToolResultMapper.RegistrationWriteFailedError());
            }

            _allowlist.Update(repositoryId, updated);
            return RepositoryUpdateOutcome.Success(repositoryId, updated, existing);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task<RepositoryUpdateOutcome> UpdateSettingsAsync(
        string repositoryId, RepositorySettingsChanges changes, CancellationToken cancellationToken,
        Func<CancellationToken, Task>? revalidate = null, bool operationGateHeld = false)
    {
        ArgumentNullException.ThrowIfNull(changes);
        string[] allowed = ["direct_push_branches", "protected_branches", "pull_branches", "tag_pattern",
            "tag_target_branch", "require_clean_working_tree", "commit_author_name", "commit_author_email"];
        if (!RepositoryId.IsLookupValid(repositoryId) || changes.Fields.Count == 0
            || changes.Fields.Any(field => !allowed.Contains(field, StringComparer.Ordinal)))
            return RepositoryUpdateOutcome.Failure(new BuckettieToolError(
                "repository_settings_invalid", "Repository settings update is invalid."));
        bool authorNameSpecified = changes.Fields.Contains("commit_author_name");
        bool authorEmailSpecified = changes.Fields.Contains("commit_author_email");
        if (authorNameSpecified != authorEmailSpecified
            || (authorNameSpecified && !GitCommitAuthor.IsValid(changes.CommitAuthorName, changes.CommitAuthorEmail)))
            return RepositoryUpdateOutcome.Failure(new BuckettieToolError(
                "repository_settings_invalid", "Repository settings update is invalid."));
        if (changes.Fields.Contains("tag_pattern")
            && (changes.TagPattern is null || !TagPatternValidator.IsValid(changes.TagPattern)))
            return RepositoryUpdateOutcome.Failure(new BuckettieToolError(
                "repository_settings_invalid", "Repository settings update is invalid."));
        if (changes.Fields.Contains("tag_target_branch") && string.IsNullOrWhiteSpace(changes.TagTargetBranch))
            return RepositoryUpdateOutcome.Failure(new BuckettieToolError(
                "repository_settings_invalid", "Repository settings update is invalid."));
        if (changes.Fields.Contains("direct_push_branches") && changes.DirectPushBranches is null
            || changes.Fields.Contains("protected_branches") && changes.ProtectedBranches is null
            || changes.Fields.Contains("pull_branches") && changes.PullBranches is null
            || changes.Fields.Contains("require_clean_working_tree") && changes.RequireCleanWorkingTree is null)
            return RepositoryUpdateOutcome.Failure(new BuckettieToolError(
                "repository_settings_invalid", "Repository settings update is invalid."));
        bool acquiredGate = false;
        if (!operationGateHeld)
        {
            if (!await _gate.TryEnterAsync(cancellationToken).ConfigureAwait(false))
                return RepositoryUpdateOutcome.Failure(BuckettieToolResultMapper.RegistrationInProgressError());
            acquiredGate = true;
        }
        try
        {
            if (!_allowlist.TryGet(repositoryId, out RepositoryOptions? existing) || existing is null)
                return RepositoryUpdateOutcome.Failure(BuckettieToolResultMapper.RegistrationValidationError(
                    RepositoryValidationError.RepositoryNotRegistered));
            RepositoryOptions updated = existing with
            {
                DirectPushBranches = changes.Fields.Contains("direct_push_branches") ? changes.DirectPushBranches! : existing.DirectPushBranches,
                ProtectedBranches = changes.Fields.Contains("protected_branches") ? changes.ProtectedBranches! : existing.ProtectedBranches,
                PullBranches = changes.Fields.Contains("pull_branches") ? changes.PullBranches! : existing.PullBranches,
                TagPattern = changes.Fields.Contains("tag_pattern") ? changes.TagPattern! : existing.TagPattern,
                TagTargetBranch = changes.Fields.Contains("tag_target_branch") ? changes.TagTargetBranch! : existing.TagTargetBranch,
                RequireCleanWorkingTree = changes.Fields.Contains("require_clean_working_tree") ? changes.RequireCleanWorkingTree!.Value : existing.RequireCleanWorkingTree,
                CommitAuthorName = authorNameSpecified ? changes.CommitAuthorName : existing.CommitAuthorName,
                CommitAuthorEmail = authorEmailSpecified ? changes.CommitAuthorEmail : existing.CommitAuthorEmail,
            };
            ApprovalPromptRequest promptRequest = new(repositoryId, existing.Workspace, existing.Slug, existing.LocalRoot,
                existing.Remote ?? BitbucketRemoteUrlValidator.RepositoryUrl(existing.Workspace, existing.Slug),
                Operation: ApprovalOperation.Update);
            ApprovalPromptOutcome approval = await _approvalPrompt.RequestApprovalAsync(promptRequest, ApprovalTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (approval.Outcome != ApprovalOutcome.Approved)
                return RepositoryUpdateOutcome.Failure(approval.Outcome == ApprovalOutcome.Denied
                    ? new BuckettieToolError("repository_settings_approval_cancelled", "Repository settings approval was cancelled.")
                    : BuckettieToolResultMapper.RegistrationApprovalError(approval.Outcome));
            if (revalidate is not null) await revalidate(cancellationToken).ConfigureAwait(false);
            if (!await _repositoryStore.UpdateAsync(repositoryId, updated, cancellationToken).ConfigureAwait(false))
                return RepositoryUpdateOutcome.Failure(new BuckettieToolError(
                    "repository_settings_write_failed", "Repository settings could not be saved."));
            _allowlist.Update(repositoryId, updated);
            return RepositoryUpdateOutcome.Success(repositoryId, updated, existing);
        }
        finally
        {
            if (acquiredGate) _gate.Release();
        }
    }
}

/// <summary>Repository修正要求の結果です。</summary>
public sealed record RepositoryUpdateOutcome(bool IsSuccess, string? RepositoryId, BuckettieToolError? Error,
    RepositoryOptions? UpdatedOptions = null, RepositoryOptions? PreviousOptions = null)
{
    /// <summary>成功結果を生成します。</summary>
    public static RepositoryUpdateOutcome Success(string repositoryId, RepositoryOptions? updated = null, RepositoryOptions? previous = null) =>
        new(true, repositoryId, null, updated, previous);

    /// <summary>失敗結果を生成します。</summary>
    public static RepositoryUpdateOutcome Failure(BuckettieToolError error) => new(false, null, error);
}
