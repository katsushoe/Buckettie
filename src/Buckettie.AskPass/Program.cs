using System.Text;
using Buckettie.Application.Git;
using Buckettie.Infrastructure.Credentials;

namespace Buckettie.AskPass;

internal static class Program
{
    private const int InvalidRequestExitCode = 2;
    private const int TokenUnavailableExitCode = 3;

    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        string? repository = Environment.GetEnvironmentVariable(GitAskPassProtocol.RepositoryVariable);
        string? username = Environment.GetEnvironmentVariable(GitAskPassProtocol.UsernameVariable);
        if (args.Length != 1 || repository is null || username is null)
        {
            return InvalidRequestExitCode;
        }

        string secretDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "secrets"));
        GitAskPassResponder responder = new(new DpapiFileTokenStore(secretDirectory));
        GitAskPassResponse response = responder.Respond(repository, username, args[0]);
        if (!response.IsSuccess || response.Value is null)
        {
            return response.Error == GitAskPassError.TokenUnavailable
                ? TokenUnavailableExitCode
                : InvalidRequestExitCode;
        }

        await Console.Out.WriteLineAsync(response.Value).ConfigureAwait(false);
        return 0;
    }
}
