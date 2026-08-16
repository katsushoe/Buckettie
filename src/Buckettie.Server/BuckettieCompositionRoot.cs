using Buckettie.Application.Configuration;
using Buckettie.Application.Credentials;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Git;
using Buckettie.Application.Repositories;
using Buckettie.Infrastructure.Configuration;
using Buckettie.Infrastructure.Credentials;
using Buckettie.Infrastructure.Bitbucket;
using Buckettie.Infrastructure.Git;
using Buckettie.Infrastructure.Repositories;
using Microsoft.Extensions.DependencyInjection;

namespace Buckettie.Server;

/// <summary>
/// Buckettie Serverの依存関係を構築します。
/// </summary>
public static class BuckettieCompositionRoot
{
    /// <summary>
    /// 設定を読み込み、実行時サービスを構築します。
    /// </summary>
    public static async Task<BuckettieCompositionResult> CreateAsync(
        Stream configuration,
        string askPassExecutable,
        TimeSpan gitCommandTimeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(askPassExecutable);

        JsonBuckettieOptionsLoader loader = new();
        ConfigurationLoadResult loadResult = await loader.LoadAsync(
            configuration,
            cancellationToken).ConfigureAwait(false);
        if (!loadResult.IsValid || loadResult.Options is null)
        {
            return new BuckettieCompositionResult(null, loadResult.Errors);
        }

        BuckettieOptions options = loadResult.Options;
        ServiceCollection services = new();
        services.AddSingleton(options);
        services.AddSingleton<IBuckettieOptionsLoader, JsonBuckettieOptionsLoader>();
        string binaryDirectory = Path.GetDirectoryName(askPassExecutable)!;
        BuckettiePathLayout paths = BuckettiePathLayout.FromBinaryDirectory(binaryDirectory);
        services.AddSingleton<IApiTokenStore>(_ => new DpapiFileTokenStore(paths.SecretDirectory));
        services.AddSingleton<IRepositoryEnvironment, SystemRepositoryEnvironment>();
        services.AddSingleton<RepositoryAllowlist>();
        services.AddSingleton<LocalPathValidator>();
        services.AddSingleton<BitbucketRemoteUrlValidator>();
        services.AddSingleton<IGitCommandClient>(_ => new GitCommandClient(
            gitCommandTimeout,
            askPassExecutable,
            options.BitbucketUsername));
        services.AddSingleton<IGitGateway, GitGateway>();
        services.AddHttpClient("Bitbucket", client =>
        {
            client.BaseAddress = new Uri("https://api.bitbucket.org/2.0/", UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(30);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            AllowAutoRedirect = false,
        });
        services.AddSingleton<IBitbucketApiClient>(provider => new BitbucketApiClient(
            provider.GetRequiredService<IHttpClientFactory>().CreateClient("Bitbucket"),
            provider.GetRequiredService<IApiTokenStore>(),
            options.AtlassianEmail));
        services.AddSingleton<IBitbucketRepositoryGateway, BitbucketRepositoryGateway>();

        ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        return new BuckettieCompositionResult(provider, Array.Empty<ConfigurationError>());
    }
}
