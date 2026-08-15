namespace Buckettie.Server;

internal static class Program
{
    private static readonly TimeSpan GitCommandTimeout = TimeSpan.FromSeconds(30);

    private static async Task<int> Main(string[] args)
    {
        string configurationPath = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.Combine(AppContext.BaseDirectory, "buckettie.json");
        string askPassExecutable = Path.Combine(AppContext.BaseDirectory, "Buckettie.AskPass.exe");

        await using FileStream configuration = File.OpenRead(configurationPath);
        using BuckettieCompositionResult result = await BuckettieCompositionRoot.CreateAsync(
            configuration,
            askPassExecutable,
            GitCommandTimeout).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return 0;
        }

        foreach (Buckettie.Application.Configuration.ConfigurationError error in result.Errors)
        {
            Console.Error.WriteLine($"{error.Code}: {error.Path}");
        }

        return 2;
    }
}
