using Buckettie.Application.Configuration;
using Buckettie.Application.Credentials;
using Buckettie.Application.Interactive;
using Buckettie.Application.Repositories;

namespace Buckettie.Server;

/// <summary>Repository登録要求を1つの流れとして実行する境界です。</summary>
public interface IRepositoryRegistrationService
{
    /// <summary>Repositoryの登録を試みます。</summary>
    Task<RepositoryRegistrationOutcome> RegisterAsync(
        string repositoryId,
        string localRoot,
        string remote,
        string developBranch,
        string mainBranch,
        CancellationToken cancellationToken);
}

/// <summary>
/// Repository登録の検証・人間承認・永続化を1つの流れとして実行します。
/// 承認済みでも、書き込みに成功するまでAllowlistへは反映しません。
/// </summary>
internal sealed class RepositoryRegistrationService : IRepositoryRegistrationService
{
    private static readonly TimeSpan ApprovalTimeout = TimeSpan.FromSeconds(120);
    private static readonly string DefaultTagPattern = "^v[0-9]+\\.[0-9]+\\.[0-9]+.*$";

    private readonly RepositoryRegistrationValidator _validator;
    private readonly RepositoryAllowlist _allowlist;
    private readonly IRepositoryStore _repositoryStore;
    private readonly IApiTokenStore _tokenStore;
    private readonly IInteractiveApprovalPrompt _approvalPrompt;
    private readonly RepositoryMutationGate _gate;

    /// <summary>登録Serviceを初期化します。</summary>
    public RepositoryRegistrationService(
        RepositoryRegistrationValidator validator,
        RepositoryAllowlist allowlist,
        IRepositoryStore repositoryStore,
        IApiTokenStore tokenStore,
        IInteractiveApprovalPrompt approvalPrompt,
        RepositoryMutationGate gate)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(allowlist);
        ArgumentNullException.ThrowIfNull(repositoryStore);
        ArgumentNullException.ThrowIfNull(tokenStore);
        ArgumentNullException.ThrowIfNull(approvalPrompt);
        ArgumentNullException.ThrowIfNull(gate);
        _validator = validator;
        _allowlist = allowlist;
        _repositoryStore = repositoryStore;
        _tokenStore = tokenStore;
        _approvalPrompt = approvalPrompt;
        _gate = gate;
    }

    /// <summary>
    /// Repositoryの登録を試みます。検証失敗・拒否・Timeout・書き込み失敗のいずれでもAllowlistは変化しません。
    /// </summary>
    public async Task<RepositoryRegistrationOutcome> RegisterAsync(
        string repositoryId,
        string localRoot,
        string remote,
        string developBranch,
        string mainBranch,
        CancellationToken cancellationToken)
    {
        if (!await _gate.TryEnterAsync(cancellationToken).ConfigureAwait(false))
        {
            return RepositoryRegistrationOutcome.Failure(BuckettieToolResultMapper.RegistrationInProgressError());
        }

        try
        {
            RepositoryRegistrationValidationResult validation = await _validator.ValidateAsync(
                repositoryId, localRoot, remote, cancellationToken).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return RepositoryRegistrationOutcome.Failure(
                    BuckettieToolResultMapper.RegistrationValidationError(validation.Error!.Value));
            }

            bool tokenRequired = !_tokenStore.Read(repositoryId).IsSuccess;
            ApprovalPromptRequest promptRequest = new(
                repositoryId,
                validation.Workspace!,
                validation.Slug!,
                validation.LocalRoot!,
                validation.RemoteUrl!,
                tokenRequired);

            ApprovalPromptOutcome approval = await _approvalPrompt
                .RequestApprovalAsync(promptRequest, ApprovalTimeout, cancellationToken)
                .ConfigureAwait(false);
            if (approval.Outcome != ApprovalOutcome.Approved)
            {
                return RepositoryRegistrationOutcome.Failure(
                    BuckettieToolResultMapper.RegistrationApprovalError(approval.Outcome));
            }

            bool tokenSaved = false;
            if (tokenRequired)
            {
                if (string.IsNullOrWhiteSpace(approval.Token))
                {
                    return RepositoryRegistrationOutcome.Failure(
                        BuckettieToolResultMapper.RegistrationTokenFailedError());
                }

                ApiTokenStoreResult tokenResult = _tokenStore.Save(repositoryId, approval.Token);
                if (!tokenResult.IsSuccess)
                {
                    return RepositoryRegistrationOutcome.Failure(
                        BuckettieToolResultMapper.RegistrationTokenFailedError());
                }

                tokenSaved = true;
            }

            RepositoryOptions newRepository = CreateServerDefaultedOptions(
                validation.Workspace!, validation.Slug!, validation.LocalRoot!, remote, developBranch, mainBranch);

            bool written = await _repositoryStore.InsertAsync(repositoryId, newRepository, cancellationToken)
                .ConfigureAwait(false);
            if (!written)
            {
                if (tokenSaved)
                {
                    _ = _tokenStore.Delete(repositoryId);
                }
                return RepositoryRegistrationOutcome.Failure(BuckettieToolResultMapper.RegistrationWriteFailedError());
            }

            _allowlist.Register(repositoryId, newRepository);
            return RepositoryRegistrationOutcome.Success(
                repositoryId, validation.Workspace!, validation.Slug!);
        }
        finally
        {
            _gate.Release();
        }
    }

    private static RepositoryOptions CreateServerDefaultedOptions(
        string workspace,
        string slug,
        string localRoot,
        string remote,
        string developBranch,
        string mainBranch) => new()
    {
        Workspace = workspace,
        Slug = slug,
        LocalRoot = localRoot,
        Remote = remote,
        DevelopBranch = developBranch,
        MainBranch = mainBranch,
        DirectPushBranches = new HashSet<string> { developBranch },
        PullBranches = new HashSet<string> { developBranch, mainBranch },
        ProtectedBranches = new HashSet<string> { mainBranch },
        TagTargetBranch = mainBranch,
        TagPattern = DefaultTagPattern,
        RequireCleanWorkingTree = true,
    };
}

/// <summary>Repository登録要求の結果です。</summary>
public sealed record RepositoryRegistrationOutcome(
    bool IsSuccess,
    string? RepositoryId,
    string? Workspace,
    string? Slug,
    BuckettieToolError? Error)
{
    /// <summary>成功結果を生成します。</summary>
    public static RepositoryRegistrationOutcome Success(string repositoryId, string workspace, string slug) =>
        new(true, repositoryId, workspace, slug, null);

    /// <summary>失敗結果を生成します。</summary>
    public static RepositoryRegistrationOutcome Failure(BuckettieToolError error) =>
        new(false, null, null, null, error);
}
