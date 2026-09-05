namespace Buckettie.Infrastructure.Git;

internal interface IProcessExecutor
{
    internal Task<ProcessExecutionResult> ExecuteAsync(
        ProcessRequest request,
        CancellationToken cancellationToken);
}

internal sealed record ProcessRequest(
    string FileName,
    string WorkingDirectory,
    IReadOnlyList<string> Arguments,
    IReadOnlyDictionary<string, string> Environment,
    TimeSpan Timeout,
    string? StandardInput = null);

internal sealed record ProcessExecutionResult(
    int? ExitCode,
    string StandardOutput,
    string StandardError,
    bool NotFound,
    bool TimedOut,
    bool Cancelled);
