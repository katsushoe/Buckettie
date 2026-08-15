namespace Buckettie.Infrastructure.Git;

internal static class GitEnvironmentSanitizer
{
    private static readonly string[] RemovedVariables =
    [
        "GIT_ALTERNATE_OBJECT_DIRECTORIES",
        "GIT_ASKPASS",
        "GIT_COMMON_DIR",
        "GIT_CONFIG_COUNT",
        "GIT_CONFIG_PARAMETERS",
        "GIT_DIR",
        "GIT_INDEX_FILE",
        "GIT_OBJECT_DIRECTORY",
        "GIT_PROXY_COMMAND",
        "GIT_SSH",
        "GIT_SSH_COMMAND",
        "GIT_TERMINAL_PROMPT",
        "GIT_WORK_TREE",
        "SSH_ASKPASS",
    ];

    internal static void Sanitize(IDictionary<string, string?> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        foreach (string variable in RemovedVariables)
        {
            environment.Remove(variable);
        }

        string[] generatedConfigVariables = environment.Keys
            .Where(name => name.StartsWith("GIT_CONFIG_KEY_", StringComparison.OrdinalIgnoreCase)
                || name.StartsWith("GIT_CONFIG_VALUE_", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        foreach (string variable in generatedConfigVariables)
        {
            environment.Remove(variable);
        }
    }
}
