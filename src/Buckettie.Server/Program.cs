using Buckettie.Application.Bitbucket;
using Buckettie.Application.Configuration;
using Buckettie.Application.Git;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Buckettie.Server;

internal static class Program
{
    private static readonly TimeSpan GitCommandTimeout = TimeSpan.FromSeconds(30);

    private static async Task<int> Main(string[] args)
    {
        string configurationPath = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "config", "buckettie.json"));
        string askPassExecutable = Path.Combine(AppContext.BaseDirectory, "Buckettie.AskPass.exe");

        await using FileStream configuration = File.OpenRead(configurationPath);
        using BuckettieCompositionResult result = await BuckettieCompositionRoot.CreateAsync(
            configuration,
            askPassExecutable,
            GitCommandTimeout).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            await RunServerAsync(result.Services!, CancellationToken.None).ConfigureAwait(false);
            return 0;
        }

        foreach (ConfigurationError error in result.Errors)
        {
            Console.Error.WriteLine($"{error.Code}: {error.Path}");
        }

        return 2;
    }

    private static async Task RunServerAsync(
        IServiceProvider buckettieServices,
        CancellationToken cancellationToken)
    {
        BuckettieOptions options = buckettieServices.GetRequiredService<BuckettieOptions>();
        WebApplicationBuilder builder = WebApplication.CreateSlimBuilder();
        string logDirectory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "logs"));
        builder.Logging.AddProvider(new DailyFileLoggerProvider(logDirectory));
        builder.Services.AddWindowsService(service => service.ServiceName = "Buckettie");
        builder.WebHost.ConfigureKestrel(server => server.ListenLocalhost(options.McpPort));
        builder.Services.AddSingleton<IBuckettieAuditLogger, BuckettieAuditLogger>();
        builder.Services.AddSingleton<IGitGateway>(provider => new AuditedGitGateway(
            buckettieServices.GetRequiredService<IGitGateway>(),
            provider.GetRequiredService<IBuckettieAuditLogger>()));
        builder.Services.AddSingleton<IBitbucketRepositoryGateway>(provider => new AuditedBitbucketRepositoryGateway(
            buckettieServices.GetRequiredService<IBitbucketRepositoryGateway>(),
            provider.GetRequiredService<IBuckettieAuditLogger>()));

        builder.Services.AddMcpServer()
            .WithHttpTransport(transport => transport.Stateless = true)
            .WithTools<BuckettieMcpTools>(BuckettieMcpJson.CreateOptions());

        await using WebApplication app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments(options.McpPath)
                && !McpOriginValidator.IsAllowed(context.Request.Headers.Origin, options.McpPort))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return;
            }

            await next(context).ConfigureAwait(false);
        });
        app.MapMcp(options.McpPath);
        await app.StartAsync(cancellationToken).ConfigureAwait(false);
        await app.WaitForShutdownAsync(cancellationToken).ConfigureAwait(false);
    }
}
