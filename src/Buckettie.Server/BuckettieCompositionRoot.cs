using Buckettie.Application.Configuration;
using Buckettie.Application.Credentials;
using Buckettie.Application.Bitbucket;
using Buckettie.Application.Git;
using Buckettie.Application.Interactive;
using Buckettie.Application.Repositories;
using Buckettie.Infrastructure.Configuration;
using Buckettie.Infrastructure.Credentials;
using Buckettie.Infrastructure.Bitbucket;
using Buckettie.Infrastructure.Git;
using Buckettie.Infrastructure.Interactive;
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
        string configurationPath,
        string askPassExecutable,
        string approvalPromptExecutable,
        TimeSpan gitCommandTimeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(askPassExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(approvalPromptExecutable);

        JsonBuckettieOptionsLoader loader = new();
        ConfigurationLoadResult loadResult = await loader.LoadAsync(
            configuration,
            cancellationToken).ConfigureAwait(false);
        if (!loadResult.IsValid || loadResult.Options is null)
        {
            return new BuckettieCompositionResult(null, loadResult.Errors);
        }

        BuckettieOptions options = loadResult.Options;
        string binaryDirectory = Path.GetDirectoryName(askPassExecutable)!;
        BuckettiePathLayout paths = BuckettiePathLayout.FromBinaryDirectory(binaryDirectory);
        string fullConfigurationPath = Path.GetFullPath(configurationPath);

        SqliteRepositoryStore repositoryStore = new(Path.Combine(paths.DataDirectory, "repositories.db"));
        options = await MigrateRepositoriesIfNeededAsync(
            options, repositoryStore, loader, fullConfigurationPath, cancellationToken).ConfigureAwait(false);

        ServiceCollection services = new();
        services.AddSingleton(options);
        services.AddSingleton<IBuckettieOptionsLoader>(loader);
        services.AddSingleton<IRepositoryStore>(repositoryStore);
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
        services.AddSingleton<RepositoryRegistrationValidator>();
        services.AddSingleton<IInteractiveApprovalPrompt>(
            _ => new WindowsInteractiveApprovalPrompt(approvalPromptExecutable, options.Language));
        services.AddSingleton<RepositoryMutationGate>();
        services.AddSingleton<IRepositoryRegistrationService>(provider => new RepositoryRegistrationService(
            provider.GetRequiredService<RepositoryRegistrationValidator>(),
            provider.GetRequiredService<RepositoryAllowlist>(),
            provider.GetRequiredService<IRepositoryStore>(),
            provider.GetRequiredService<IInteractiveApprovalPrompt>(),
            provider.GetRequiredService<RepositoryMutationGate>()));
        services.AddSingleton<IRepositoryUnregistrationService>(provider => new RepositoryUnregistrationService(
            provider.GetRequiredService<RepositoryAllowlist>(),
            provider.GetRequiredService<IRepositoryStore>(),
            provider.GetRequiredService<RepositoryMutationGate>()));
        services.AddSingleton<IRepositoryUpdateService>(provider => new RepositoryUpdateService(
            provider.GetRequiredService<RepositoryAllowlist>(),
            provider.GetRequiredService<IRepositoryStore>(),
            provider.GetRequiredService<IInteractiveApprovalPrompt>(),
            provider.GetRequiredService<RepositoryMutationGate>()));

        ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true,
            });
        return new BuckettieCompositionResult(provider, Array.Empty<ConfigurationError>());
    }

    /// <summary>
    /// buckettie.json由来のRepository設定をSQLite Storeへ一度だけ移行し、以降はStoreを正本とします。
    /// Storeが既に何か保持している場合、JSON側のrepositoriesは無視します。
    /// </summary>
    private static async Task<BuckettieOptions> MigrateRepositoriesIfNeededAsync(
        BuckettieOptions options,
        IRepositoryStore repositoryStore,
        IBuckettieOptionsLoader loader,
        string configurationPath,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<string, RepositoryOptions> stored = await repositoryStore
            .LoadAllAsync(cancellationToken).ConfigureAwait(false);
        if (stored.Count == 0 && options.Repositories.Count > 0)
        {
            foreach ((string repositoryId, RepositoryOptions repository) in options.Repositories)
            {
                await repositoryStore.InsertAsync(repositoryId, repository, cancellationToken).ConfigureAwait(false);
            }

            await ConfigurationFileWriter.SaveAtomicallyAsync(
                loader,
                options with { Repositories = new Dictionary<string, RepositoryOptions>() },
                configurationPath,
                cancellationToken).ConfigureAwait(false);
            stored = await repositoryStore.LoadAllAsync(cancellationToken).ConfigureAwait(false);
        }

        return options with { Repositories = stored };
    }
}
