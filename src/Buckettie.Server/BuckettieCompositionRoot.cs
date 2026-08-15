using Buckettie.Application.Configuration;
using Buckettie.Application.Credentials;
using Buckettie.Application.Git;
using Buckettie.Application.Repositories;
using Buckettie.Infrastructure.Configuration;
using Buckettie.Infrastructure.Credentials;
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
        services.AddSingleton<IApiTokenStore, WindowsCredentialManagerTokenStore>();
        services.AddSingleton<IRepositoryEnvironment, SystemRepositoryEnvironment>();
        services.AddSingleton<RepositoryAllowlist>();
        services.AddSingleton<LocalPathValidator>();
        services.AddSingleton<BitbucketRemoteUrlValidator>();
        services.AddSingleton<IGitCommandClient>(_ => new GitCommandClient(
            gitCommandTimeout,
            askPassExecutable,
            options.AtlassianEmail));
        services.AddSingleton<IGitGateway, GitGateway>();

        ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        return new BuckettieCompositionResult(provider, Array.Empty<ConfigurationError>());
    }
}
