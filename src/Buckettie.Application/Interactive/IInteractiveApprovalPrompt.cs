namespace Buckettie.Application.Interactive;

/// <summary>
/// 対話セッションの人間へRepository登録の承認を要求します。
/// </summary>
public interface IInteractiveApprovalPrompt
{
    /// <summary>
    /// 承認Dialogを表示し、結果を待ちます。
    /// </summary>
    Task<ApprovalPromptOutcome> RequestApprovalAsync(
        ApprovalPromptRequest request,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

/// <summary>承認要求の結果種別です。</summary>
public enum ApprovalOutcome
{
    Approved,
    Denied,
    TimedOut,
    NoInteractiveSession,
    LaunchFailed,
    ProtocolError,
}

/// <summary>
/// 承認Dialogへ提示するRepository登録候補です（Secretは含みません）。
/// </summary>
public sealed record ApprovalPromptRequest(
    string RepositoryId,
    string Workspace,
    string Slug,
    string LocalRoot,
    string RemoteUrl,
    string Language = "auto");

/// <summary>承認要求の結果です。</summary>
public sealed record ApprovalPromptOutcome(ApprovalOutcome Outcome)
{
    /// <summary>承認済み結果を生成します。</summary>
    public static ApprovalPromptOutcome Approved() => new(ApprovalOutcome.Approved);

    /// <summary>拒否済み結果を生成します。</summary>
    public static ApprovalPromptOutcome Denied() => new(ApprovalOutcome.Denied);

    /// <summary>指定した理由での失敗結果を生成します。</summary>
    public static ApprovalPromptOutcome Failure(ApprovalOutcome outcome)
    {
        if (outcome is ApprovalOutcome.Approved or ApprovalOutcome.Denied)
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        return new(outcome);
    }
}
