using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;

namespace Buckettie.Infrastructure.Configuration;

/// <summary>
/// snake_case JSONからBuckettie設定を読み込みます。
/// </summary>
public sealed class JsonBuckettieOptionsLoader : IBuckettieOptionsLoader
{
    private static readonly string[] RequiredRepositoryProperties =
    [
        "workspace",
        "slug",
        "local_root",
        "remote",
        "develop_branch",
        "main_branch",
        "direct_push_branches",
        "pull_branches",
        "protected_branches",
        "tag_target_branch",
        "tag_pattern",
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <inheritdoc />
    public async Task<ConfigurationLoadResult> LoadAsync(
        Stream json,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            using JsonDocument document = await JsonDocument.ParseAsync(
                json,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            ConfigurationError? structureError = ValidateStructure(document.RootElement);
            if (structureError is not null)
            {
                return ConfigurationLoadResult.Failure(structureError);
            }

            BuckettieOptions? options = document.RootElement.Deserialize<BuckettieOptions>(SerializerOptions);
            if (options is null)
            {
                return InvalidJson();
            }

            ConfigurationError? valueError = ValidateValues(options);
            return valueError is null
                ? ConfigurationLoadResult.Success(options)
                : ConfigurationLoadResult.Failure(valueError);
        }
        catch (JsonException)
        {
            return InvalidJson();
        }
    }

    private static ConfigurationError? ValidateStructure(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("atlassian_email", out JsonElement email)
            || email.ValueKind != JsonValueKind.String
            || !root.TryGetProperty("repositories", out JsonElement repositories)
            || repositories.ValueKind != JsonValueKind.Object)
        {
            return Required("repositories");
        }

        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (JsonProperty repository in repositories.EnumerateObject())
        {
            if (!RepositoryId.IsValid(repository.Name))
            {
                return new(ConfigurationErrorCode.InvalidRepositoryId, $"repositories.{repository.Name}");
            }

            if (!ids.Add(repository.Name))
            {
                return new(ConfigurationErrorCode.DuplicateRepositoryId, $"repositories.{repository.Name}");
            }

            ConfigurationError? error = ValidateRepositoryStructure(repository);
            if (error is not null)
            {
                return error;
            }
        }

        return null;
    }

    private static ConfigurationError? ValidateRepositoryStructure(JsonProperty repository)
    {
        if (repository.Value.ValueKind != JsonValueKind.Object)
        {
            return Required($"repositories.{repository.Name}");
        }

        foreach (string propertyName in RequiredRepositoryProperties)
        {
            if (!repository.Value.TryGetProperty(propertyName, out _))
            {
                return Required($"repositories.{repository.Name}.{propertyName}");
            }
        }

        return null;
    }

    private static ConfigurationError? ValidateValues(BuckettieOptions options)
    {
        if (!AtlassianEmail.IsValid(options.AtlassianEmail))
        {
            return new(ConfigurationErrorCode.InvalidAtlassianEmail, "atlassian_email");
        }

        foreach ((string id, RepositoryOptions repository) in options.Repositories)
        {
            string prefix = $"repositories.{id}";
            ConfigurationError? requiredError = FirstMissingValue(repository, prefix);
            if (requiredError is not null)
            {
                return requiredError;
            }

            try
            {
                _ = new Regex(
                    repository.TagPattern,
                    RegexOptions.CultureInvariant,
                    TimeSpan.FromSeconds(1));
            }
            catch (ArgumentException)
            {
                return new(ConfigurationErrorCode.InvalidTagPattern, $"{prefix}.tag_pattern");
            }
        }

        return null;
    }

    private static ConfigurationError? FirstMissingValue(RepositoryOptions repository, string prefix)
    {
        (string Name, object? Value)[] requiredValues =
        [
            ("workspace", repository.Workspace),
            ("slug", repository.Slug),
            ("local_root", repository.LocalRoot),
            ("remote", repository.Remote),
            ("develop_branch", repository.DevelopBranch),
            ("main_branch", repository.MainBranch),
            ("direct_push_branches", repository.DirectPushBranches),
            ("pull_branches", repository.PullBranches),
            ("protected_branches", repository.ProtectedBranches),
            ("tag_target_branch", repository.TagTargetBranch),
            ("tag_pattern", repository.TagPattern),
        ];

        foreach ((string name, object? value) in requiredValues)
        {
            if (value is null || value is string text && string.IsNullOrWhiteSpace(text))
            {
                return Required($"{prefix}.{name}");
            }
        }

        return null;
    }

    private static ConfigurationError Required(string path) =>
        new(ConfigurationErrorCode.RequiredValueMissing, path);

    private static ConfigurationLoadResult InvalidJson() =>
        ConfigurationLoadResult.Failure(new ConfigurationError(ConfigurationErrorCode.InvalidJson, "$"));
}
