using Buckettie.Application.Bitbucket;
using Buckettie.Application.Configuration;
using Buckettie.Application.Credentials;
using Buckettie.Application.Git;
using Buckettie.Server;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Buckettie.Cli;

internal static class Program
{
    private static Task<int> Main(string[] args) => CliApplication.RunAsync(
        args, Console.Out, Console.Error, secretReader: ReadSecret);

    private static string? ReadSecret()
    {
        StringBuilder value = new();
        while (true)
        {
            ConsoleKeyInfo key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) { Console.Error.WriteLine(); return value.ToString(); }
            if (key.Key == ConsoleKey.Escape) return null;
            if (key.Key == ConsoleKey.Backspace && value.Length > 0) value.Length--;
            else if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
        }
    }
}

internal static class CliApplication
{
    private static readonly TimeSpan GitTimeout = TimeSpan.FromSeconds(30);

    internal static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error,
        CancellationToken cancellationToken = default,
        IServiceCommandExecutor? serviceExecutor = null,
        Func<string?>? secretReader = null)
    {
        ArgumentNullException.ThrowIfNull(args);
        string configPath = GetConfigPath(args);
        string[] command = RemoveConfigOption(args);
        if (command.Length == 0 || command[0] is "help" or "--help" or "-h")
        {
            WriteHelp(output);
            return 0;
        }

        if (command[0] == "version")
        {
            output.WriteLine(Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown");
            return 0;
        }

        if (command[0] == "logs")
        {
            BuckettiePathLayout paths = BuckettiePathLayout.FromBinaryDirectory(AppContext.BaseDirectory);
            output.WriteLine(paths.LogDirectory);
            return 0;
        }

        WindowsServiceManager serviceManager = new(
            serviceExecutor ?? new ScServiceCommandExecutor(),
            AppContext.BaseDirectory);
        int serviceExitCode = await serviceManager.ExecuteAsync(command, output, cancellationToken).ConfigureAwait(false);
        if (serviceExitCode >= 0) return serviceExitCode;

        BuckettieCompositionResult composition;
        try
        {
            await using FileStream stream = File.OpenRead(configPath);
            composition = await BuckettieCompositionRoot.CreateAsync(
                stream,
                configPath,
                Path.Combine(AppContext.BaseDirectory, "Buckettie.AskPass.exe"),
                Path.Combine(AppContext.BaseDirectory, "Buckettie.ApprovalPrompt.exe"),
                GitTimeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"[NG] Config: {exception.GetType().Name}");
            return 2;
        }

        using (composition)
        {
            if (!composition.IsSuccess)
            {
                foreach (ConfigurationError item in composition.Errors)
                {
                    error.WriteLine($"[NG] Config: {item.Code} ({item.Path})");
                }
                return 2;
            }

            IServiceProvider services = composition.Services!;
            return command switch
            {
                ["config", "check"] => WriteOk(output, "Config"),
                ["config", "show"] => ShowConfig(services, output),
                ["repo", "list"] => ListRepositories(services, output),
                ["repo", "status", var repository] => await RepositoryStatusAsync(services, repository, output, cancellationToken).ConfigureAwait(false),
                ["auth", "test"] => TestAuthentication(services, output),
                ["auth", "set", var repository] => SetAuthentication(services, repository, output, error, secretReader),
                ["auth", "delete", var repository] => DeleteAuthentication(services, repository, output),
                ["mcp", "status"] => await TestMcpAsync(services, output, false, cancellationToken).ConfigureAwait(false),
                ["mcp", "test"] => await TestMcpAsync(services, output, false, cancellationToken).ConfigureAwait(false),
                ["mcp", "tools"] => await TestMcpAsync(services, output, true, cancellationToken).ConfigureAwait(false),
                ["doctor"] => await DoctorAsync(services, output, cancellationToken).ConfigureAwait(false),
                _ => UnknownCommand(error),
            };
        }
    }

    private static async Task<int> DoctorAsync(IServiceProvider services, TextWriter output, CancellationToken cancellationToken)
    {
        int failures = 0;
        failures += Check(output, "Config", true);
        failures += Check(output, "Git", await IsGitAvailableAsync(cancellationToken).ConfigureAwait(false));
        BuckettieOptions options = services.GetRequiredService<BuckettieOptions>();
        IApiTokenStore tokens = services.GetRequiredService<IApiTokenStore>();
        IGitGateway git = services.GetRequiredService<IGitGateway>();
        IBitbucketRepositoryGateway bitbucket = services.GetRequiredService<IBitbucketRepositoryGateway>();
        foreach (string repository in options.Repositories.Keys.Order(StringComparer.Ordinal))
        {
            ApiTokenStoreResult token = tokens.Read(repository);
            failures += Check(output, $"API Token: {repository}", token.IsSuccess, token.Error?.ToString());
            GitGatewayResult local = await git.GetStatusAsync(repository, cancellationToken).ConfigureAwait(false);
            failures += Check(output, $"Repository: {repository}", local.IsSuccess, local.Error?.ToString());
            BitbucketResult<BitbucketRepositoryInfo> remote = await bitbucket.GetRepositoryAsync(repository, cancellationToken).ConfigureAwait(false);
            failures += Check(output, $"Bitbucket API: {repository}", remote.IsSuccess, remote.Error?.ToString());
        }
        bool mcp = await IsMcpAvailableAsync(options, cancellationToken).ConfigureAwait(false);
        failures += Check(output, "MCP Endpoint", mcp);
        return failures == 0 ? 0 : 1;
    }

    private static int TestAuthentication(IServiceProvider services, TextWriter output)
    {
        BuckettieOptions options = services.GetRequiredService<BuckettieOptions>();
        IApiTokenStore tokens = services.GetRequiredService<IApiTokenStore>();
        int failures = 0;
        foreach (string repository in options.Repositories.Keys.Order(StringComparer.Ordinal))
        {
            ApiTokenStoreResult result = tokens.Read(repository);
            failures += Check(output, $"API Token: {repository}", result.IsSuccess, result.Error?.ToString());
        }
        return failures == 0 ? 0 : 1;
    }

    private static int SetAuthentication(IServiceProvider services, string repository, TextWriter output,
        TextWriter error, Func<string?>? secretReader)
    {
        if (!services.GetRequiredService<BuckettieOptions>().Repositories.ContainsKey(repository))
        {
            output.WriteLine($"[NG] API Token: {repository} (RepositoryNotAllowed)");
            return 1;
        }
        error.Write("Token: ");
        string? token = secretReader?.Invoke();
        ApiTokenStoreResult result = token is null
            ? ApiTokenStoreResult.Failure(ApiTokenStoreError.InvalidToken)
            : services.GetRequiredService<IApiTokenStore>().Save(repository, token);
        output.WriteLine($"[{(result.IsSuccess ? "OK" : "NG")}] API Token: {repository}{(result.Error is null ? string.Empty : $" ({result.Error})")}");
        return result.IsSuccess ? 0 : 1;
    }

    private static int DeleteAuthentication(IServiceProvider services, string repository, TextWriter output)
    {
        ApiTokenStoreResult result = services.GetRequiredService<IApiTokenStore>().Delete(repository);
        output.WriteLine($"[{(result.IsSuccess ? "OK" : "NG")}] API Token deleted: {repository}");
        return result.IsSuccess ? 0 : 1;
    }

    private static async Task<int> RepositoryStatusAsync(IServiceProvider services, string repository,
        TextWriter output, CancellationToken cancellationToken)
    {
        GitGatewayResult result = await services.GetRequiredService<IGitGateway>()
            .GetStatusAsync(repository, cancellationToken).ConfigureAwait(false);
        if (!result.IsSuccess)
        {
            output.WriteLine($"[NG] Repository: {repository} ({result.Error})");
            return 1;
        }
        output.WriteLine($"[OK] Repository: {repository}");
        output.WriteLine($"branch={result.Branch ?? "-"} head={result.Status?.LocalHead ?? "-"} clean={result.Status?.WorkingTreeClean}");
        return 0;
    }

    private static int ListRepositories(IServiceProvider services, TextWriter output)
    {
        foreach (string id in services.GetRequiredService<BuckettieOptions>().Repositories.Keys.Order(StringComparer.Ordinal)) output.WriteLine(id);
        return 0;
    }

    private static int ShowConfig(IServiceProvider services, TextWriter output)
    {
        BuckettieOptions options = services.GetRequiredService<BuckettieOptions>();
        output.WriteLine(JsonSerializer.Serialize(options, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
        return 0;
    }

    private static async Task<int> TestMcpAsync(IServiceProvider services, TextWriter output, bool tools, CancellationToken cancellationToken)
    {
        BuckettieOptions options = services.GetRequiredService<BuckettieOptions>();
        string method = tools ? "tools/list" : "initialize";
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(5) };
        using HttpRequestMessage request = new(HttpMethod.Post, $"http://127.0.0.1:{options.McpPort}{options.McpPath}");
        request.Headers.Accept.ParseAdd("application/json, text/event-stream");
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            jsonrpc = "2.0", id = 1, method,
            @params = tools ? (object)new { } : new { protocolVersion = "2025-06-18", capabilities = new { }, clientInfo = new { name = "buckettie-cli", version = "1" } },
        }), Encoding.UTF8, "application/json");
        try
        {
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            bool ok = response.IsSuccessStatusCode;
            output.WriteLine($"[{(ok ? "OK" : "NG")}] MCP {(tools ? "Tools" : "Endpoint")} ({(int)response.StatusCode})");
            if (tools && ok) output.WriteLine(await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false));
            return ok ? 0 : 1;
        }
        catch (HttpRequestException)
        {
            output.WriteLine($"[NG] MCP {(tools ? "Tools" : "Endpoint")}");
            return 1;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            output.WriteLine($"[NG] MCP {(tools ? "Tools" : "Endpoint")} (Timeout)");
            return 1;
        }
    }

    private static async Task<bool> IsMcpAvailableAsync(BuckettieOptions options, CancellationToken cancellationToken)
    {
        using HttpClient client = new() { Timeout = TimeSpan.FromSeconds(2) };
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, $"http://127.0.0.1:{options.McpPort}{options.McpPath}");
            using HttpResponseMessage response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            return response.StatusCode is System.Net.HttpStatusCode.MethodNotAllowed or System.Net.HttpStatusCode.BadRequest || response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested) { return false; }
    }

    private static async Task<bool> IsGitAvailableAsync(CancellationToken cancellationToken)
    {
        try
        {
            using System.Diagnostics.Process process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("git", "--version")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            })!;
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception or InvalidOperationException) { return false; }
    }

    private static int Check(TextWriter output, string name, bool success, string? detail = null)
    {
        output.WriteLine($"[{(success ? "OK" : "NG")}] {name}{(detail is null ? string.Empty : $" ({detail})")}");
        return success ? 0 : 1;
    }

    private static int WriteOk(TextWriter output, string name) { output.WriteLine($"[OK] {name}"); return 0; }
    private static int UnknownCommand(TextWriter error) { error.WriteLine("Unknown command. Run 'buckettie help'."); return 2; }

    private static string GetConfigPath(string[] args)
    {
        int index = Array.IndexOf(args, "--config");
        return index >= 0 && index + 1 < args.Length
            ? Path.GetFullPath(args[index + 1])
            : Path.Combine(
                BuckettiePathLayout.FromBinaryDirectory(AppContext.BaseDirectory).ConfigurationDirectory,
                "buckettie.json");
    }

    private static string[] RemoveConfigOption(string[] args)
    {
        int index = Array.IndexOf(args, "--config");
        return index < 0 ? args : args.Where((_, position) => position != index && position != index + 1).ToArray();
    }

    private static void WriteHelp(TextWriter output) => output.WriteLine("""
        buckettie doctor
        buckettie start|stop|restart|status
        buckettie service install|uninstall|status
        buckettie config check|show
        buckettie repo list|status <repository>
        buckettie auth test
        buckettie auth set|delete <repository>
        buckettie mcp status|tools|test
        buckettie logs
        buckettie version
        Global option: --config <path>
        """);
}
