using System.Text.Json;
using Buckettie.Application.Configuration;
using Buckettie.Application.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moyai.ProviderAuthentication;

namespace Buckettie.Server;

/// <summary>実Tool呼び出しより前に共通Validatorと登録Contextを検証します。</summary>
internal sealed class ProviderAuthenticationBoundary
{
    private const int MaximumRequestBytes = 1_048_576;
    private static readonly TimeSpan OperationWaitTimeout = TimeSpan.FromSeconds(10);
    private static readonly object DirectReadKey = new();
    private static readonly object StandaloneKey = new();
    private readonly BuckettieOptions _options;
    private readonly RepositoryAllowlist _repositories;
    private readonly IAssertionValidator? _validator;
    private readonly ILogger<ProviderAuthenticationBoundary> _logger;
    private readonly RepositoryMutationGate _gate;
    private readonly bool _directUnrestricted;

    /// <summary>構成済みValidatorをHTTP入口へ接続します。</summary>
    /// <param name="directUnrestricted">
    /// 連携モードで、Authorizationなしのloopback直接接続にすべてのRepository Toolを許可するならtrueです。
    /// </param>
    public ProviderAuthenticationBoundary(BuckettieOptions options, RepositoryAllowlist repositories,
        IAssertionValidator? validator, ILogger<ProviderAuthenticationBoundary> logger, RepositoryMutationGate? gate = null,
        bool directUnrestricted = false)
    {
        _directUnrestricted = directUnrestricted;
        _options = options;
        _repositories = repositories;
        _validator = validator;
        _logger = logger;
        _gate = gate ?? new RepositoryMutationGate();
    }

    /// <summary>共有Replay Cacheを初期化し、共通Validatorを組み立てます。</summary>
    /// <param name="moyaiIntegration">
    /// <c>--moyai</c>で起動した連携モードならtrueです。falseの単体動作では認証設定があっても使いません。
    /// </param>
    /// <param name="directUnrestricted"><c>--direct-unrestricted</c>で起動した場合はtrueです。</param>
    public static async Task<ProviderAuthenticationBoundary> CreateAsync(BuckettieOptions options,
        RepositoryAllowlist repositories, ILogger<ProviderAuthenticationBoundary> logger, CancellationToken cancellationToken,
        RepositoryMutationGate? gate = null, bool moyaiIntegration = false, bool directUnrestricted = false)
    {
        IAssertionValidator? validator = null;
        if (moyaiIntegration)
        {
            // Integration mode must never silently degrade to standalone when its configuration is missing.
            ProviderAuthenticationOptions auth = options.ProviderAuthentication ?? throw Error("authentication_unavailable");
            ValidateConfiguration(auth);
            SqliteAssertionReplayCache replay = new(new SqliteAssertionReplayCacheOptions(auth.ReplayDatabasePath));
            await replay.InitializeAsync(cancellationToken).ConfigureAwait(false);
            validator = new Es256AssertionValidator(new FileAssertionTrustStore(auth.TrustBundlePath), replay,
                new AssertionOptions(auth.Issuer), ProviderToolPolicy.Capability);
        }
        return new(options, repositories, validator, logger, gate, directUnrestricted);
    }

    /// <summary>認証失敗時は次のMiddlewareを実行しません。</summary>
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (!context.Request.Path.StartsWithSegments(_options.McpPath))
        {
            await next(context).ConfigureAwait(false);
            return;
        }
        JsonElement? requestId = null;
        bool dispatched = false;
        bool operationGateHeld = false;
        try
        {
            if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsDelete(context.Request.Method))
            {
                // Streamable HTTP session requests carry no tool call; rejecting them with 401 makes clients
                // start OAuth discovery instead of reporting a usable error.
                context.Request.Headers.Remove("Authorization");
                dispatched = true;
                await next(context).ConfigureAwait(false);
                return;
            }
            if (context.Request.Method != HttpMethods.Post) throw Error("mcp_method_not_allowed");
            if (context.Request.ContentLength > MaximumRequestBytes) throw Error("auth_assertion_invalid");
            // Memory-only buffering avoids writing Assertion-bearing malicious request bodies to disk.
            using MemoryStream body = new();
            byte[] buffer = new byte[8192];
            int count;
            while ((count = await context.Request.Body.ReadAsync(buffer, context.RequestAborted).ConfigureAwait(false)) != 0)
            {
                if (body.Length + count > MaximumRequestBytes) throw Error("auth_assertion_invalid");
                await body.WriteAsync(buffer.AsMemory(0, count), context.RequestAborted).ConfigureAwait(false);
            }
            body.Position = 0;
            using JsonDocument document = await JsonDocument.ParseAsync(body, cancellationToken: context.RequestAborted).ConfigureAwait(false);
            JsonElement root = document.RootElement;
            RequireUniqueProperties(root);
            if (root.ValueKind != JsonValueKind.Object) throw Error("auth_assertion_invalid");
            if (root.TryGetProperty("id", out JsonElement id) && id.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                requestId = id.Clone();
            if (root.GetProperty("jsonrpc").GetString() != "2.0") throw Error("auth_assertion_invalid");
            string? method = root.GetProperty("method").GetString();
            if (method == "tools/call")
            {
                if (requestId is null) throw Error("auth_assertion_invalid");
                JsonElement parameters = root.GetProperty("params");
                string tool = parameters.GetProperty("name").GetString() ?? throw Error("auth_scope_denied");
                if (ProviderToolPolicy.Scopes(tool) is not null)
                {
                    operationGateHeld = await _gate.TryEnterAsync(OperationWaitTimeout, context.RequestAborted).ConfigureAwait(false);
                    if (!operationGateHeld)
                    {
                        await BusyAsync(context, requestId).ConfigureAwait(false);
                        return;
                    }
                }
                await AuthorizeToolAsync(context, parameters).ConfigureAwait(false);
            }
            else if (!IsDiscoveryMethod(method)) throw Error("mcp_method_not_allowed");
            // Neither MCP handlers nor downstream logs need the credential.
            context.Request.Headers.Remove("Authorization");
            body.Position = 0;
            Stream original = context.Request.Body;
            context.Request.Body = body;
            try { dispatched = true; await next(context).ConfigureAwait(false); }
            finally { context.Request.Body = original; }
        }
        catch (ProviderAuthenticationException exception) when (!dispatched)
        {
            await RejectAsync(context, requestId, exception.Code).ConfigureAwait(false);
        }
        catch (Exception exception) when (!dispatched && exception is (JsonException or InvalidOperationException or KeyNotFoundException
            or ArgumentException or IOException or UnauthorizedAccessException or NullReferenceException))
        {
            // Parser and IO exception text can contain credentials or paths; log only a stable public code.
            await RejectAsync(context, requestId, "authentication_unavailable").ConfigureAwait(false);
        }
        finally
        {
            if (operationGateHeld) _gate.Release();
        }
    }

    private async Task AuthorizeToolAsync(HttpContext context, JsonElement parameters)
    {
        string tool = parameters.GetProperty("name").GetString() ?? throw Error("auth_scope_denied");
        if (ProviderToolPolicy.IsBootstrap(tool)) return;
        if (IsDirectRead(context, tool))
        {
            context.Items[DirectReadKey] = tool;
            _logger.LogInformation("[ProviderAuth] direct_read {Tool}", tool);
            return;
        }
        if (ProviderToolPolicy.IsDirectAdministration(tool) && IsDirectLoopback(context))
        {
            // The registration services require interactive desktop approval before any change.
            _logger.LogInformation("[ProviderAuth] direct_administration {Tool}", tool);
            return;
        }
        if ((_validator is null || _directUnrestricted) && ProviderToolPolicy.Scopes(tool) is not null
            && IsDirectLoopback(context))
        {
            // Buckettie must work without Moyai: unless started with --moyai, local clients use every
            // repository tool under the gateway's own allowlist, branch policies and audit log.
            // --direct-unrestricted keeps that for header-less loopback calls in integration mode;
            // IsDirectLoopback excludes any request carrying Authorization, so a failed Assertion never lands here.
            context.Items[StandaloneKey] = tool;
            _logger.LogInformation("[ProviderAuth] standalone {Tool}", tool);
            return;
        }
        if (ProviderToolPolicy.IsAdministrator(tool))
        {
            AdministratorEndpointOptions? admin = _options.AdministratorEndpoint;
            if (admin is null || !context.Request.IsHttps || context.Connection.LocalPort != admin.Port
                || !AdministratorCertificates.IsAllowed(await context.Connection.GetClientCertificateAsync(context.RequestAborted)
                    .ConfigureAwait(false), admin.ClientCertificateThumbprints)) throw Error("auth_administrator_required");
            return;
        }
        string[] scopes = ProviderToolPolicy.Scopes(tool) ?? throw Error("provider_capability_missing");
        if (_validator is null || _options.ProviderAuthentication is not { } auth) throw Error("authentication_unavailable");
        string token = Bearer(context);
        string repository = parameters.GetProperty("arguments").GetProperty("repository").GetString()
            ?? throw Error("auth_project_mismatch");
        Guid project = Guid.Empty;
        string normalized = string.Empty;
        bool bindingMissing = false;
        if (_repositories.TryGet(repository, out RepositoryOptions? registered) && registered is not null)
        {
            normalized = MoyaiBindings.Normalize(registered);
            MoyaiBinding? binding = MoyaiBindings.Resolve(repository, registered, auth);
            bindingMissing = binding is null;
            if (binding is not null && string.Equals(normalized, binding.NormalizedRepository, StringComparison.Ordinal))
                project = binding.ProjectId;
        }
        string operationId = context.Request.Headers["X-Moyai-Operation-Id"].ToString();
        if (operationId.Length is < 1 or > 200 || operationId.Any(character => !char.IsAsciiLetterOrDigit(character)
            && character is not ('-' or '_' or '.'))) throw Error("auth_assertion_invalid");
        // An unresolved context cannot match any valid Project claim. Still validate the signature first
        // so unauthenticated callers cannot distinguish existing registrations by the public error.
        AssertionContext expected = AssertionContext.ForRepository("buckettie", project, normalized, tool, scopes, operationId);
        AssertionPrincipal principal;
        try
        {
            principal = await _validator.ValidateAsync(token, expected, context.RequestAborted).ConfigureAwait(false);
        }
        catch (ProviderAuthenticationException exception) when (bindingMissing && exception.Code == "auth_project_mismatch")
        {
            // Project mismatch is reported only after the signature, issuer and audience were accepted, so naming
            // the missing binding tells nothing to a caller that Moyai did not authenticate.
            _logger.LogWarning("[ProviderAuth] binding_missing {Repository} {Tool}", repository, tool);
            throw Error("auth_binding_missing");
        }
        context.Items[typeof(AssertionPrincipal)] = principal;
        context.Items[typeof(IAssertionExecutionValidator)] = _validator as IAssertionExecutionValidator;
        context.Response.Headers["X-Moyai-Operation-Id"] = principal.Context.OperationId;
        _logger.LogInformation("[ProviderAuth] accepted {OperationId} {ProjectId} {Tool} {KeyId}",
            principal.Context.OperationId, principal.Context.Project, tool, principal.KeyId);
    }

    /// <summary>
    /// Assertionを伴わない読み取りだけを許可します。Assertionを提示した要求は検証失敗時もここへ退避しません。
    /// </summary>
    private bool IsDirectRead(HttpContext context, string tool) =>
        ProviderToolPolicy.IsDirectRead(tool) && IsDirectLoopback(context);

    /// <summary>
    /// Tool実行を伴わないMCPの初期化・一覧・通知です。MCP C# SDK 2.2以降のclientは
    /// <c>server/discover</c>で接続を開始するため、これも含めます。
    /// </summary>
    internal static bool IsDiscoveryMethod(string? method) =>
        method is "initialize" or "server/discover" or "ping" or "tools/list" or "prompts/list" or "prompts/get"
            or "resources/list" or "resources/templates/list"
        || (method is not null && method.StartsWith("notifications/", StringComparison.Ordinal));

    /// <summary>
    /// loopbackから通常のMCP portへ届いた直接接続かを判定します。連携モードではAssertionを伴わない要求に限ります。
    /// 単体モードではAuthorizationを使わないため、Moyaiが付与したAssertionがあっても直接接続と同じ扱いにします
    /// （無ヘッダーのloopback要求より権限が広がることはありません）。
    /// </summary>
    private bool IsDirectLoopback(HttpContext context) =>
        (_validator is null || context.Request.Headers.Authorization.Count == 0)
        && context.Connection.RemoteIpAddress is { } remote && System.Net.IPAddress.IsLoopback(remote)
        && context.Connection.LocalPort == _options.McpPort;

    /// <summary>実処理直前に期限と最新Trustを再確認します。Replay予約は繰り返しません。</summary>
    /// <param name="context">現在の要求。</param>
    /// <param name="operation">実行しようとしているGateway操作名。</param>
    internal static async Task EnsureCurrentAsync(HttpContext? context, string operation)
    {
        try
        {
            // A direct read may only reach read-only gateway operations, never a mutation.
            if (context?.Items[DirectReadKey] is string && ProviderToolPolicy.IsDirectRead(operation)) return;
            if (context?.Items[StandaloneKey] is string) return;
            if (context?.Items[typeof(AssertionPrincipal)] is not AssertionPrincipal principal
                || context.Items[typeof(IAssertionExecutionValidator)] is not IAssertionExecutionValidator validator)
                throw Error("authentication_unavailable");
            await validator.EnsureCurrentAsync(principal, context.RequestAborted).ConfigureAwait(false);
        }
        catch (ProviderAuthenticationException exception)
        {
            // MCP has already dispatched the request; return a stable tool error without credential details.
            throw new ModelContextProtocol.McpException(exception.Code);
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException
            or InvalidOperationException or ArgumentException or NullReferenceException)
        {
            throw new ModelContextProtocol.McpException("authentication_unavailable");
        }
    }

    private static string Bearer(HttpContext context)
    {
        var header = context.Request.Headers.Authorization;
        if (header.Count != 1 || header[0] is not { } value || !value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            throw Error("auth_assertion_missing");
        if (value.Length > 16400) throw Error("auth_assertion_invalid");
        return value[7..];
    }

    private async Task BusyAsync(HttpContext context, JsonElement? id)
    {
        const string code = "repository_busy";
        _logger.LogWarning("[RepositoryGate] rejected {Code}", code);
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.Headers.RetryAfter = "1";
        await context.Response.WriteAsJsonAsync(new { jsonrpc = "2.0", id,
            error = new { code = -32002, message = code,
                data = new { code, retryable = true, retry_after_seconds = 1, outcome = "not_executed" } } },
            context.RequestAborted).ConfigureAwait(false);
    }

    private async Task RejectAsync(HttpContext context, JsonElement? id, string code)
    {
        _logger.LogWarning("[ProviderAuth] rejected {Code}", code);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { jsonrpc = "2.0", id,
            error = new { code = -32001, message = code, data = new { code, retryable = false } } },
            context.RequestAborted).ConfigureAwait(false);
    }

    private static void RequireUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw Error("auth_assertion_invalid");
                RequireUniqueProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (JsonElement item in element.EnumerateArray()) RequireUniqueProperties(item);
    }

    private static void ValidateConfiguration(ProviderAuthenticationOptions auth)
    {
        new AssertionOptions(auth.Issuer).Validate();
        if (!Path.IsPathFullyQualified(auth.TrustBundlePath) || !Path.IsPathFullyQualified(auth.ReplayDatabasePath)
            || auth.Bindings is null || auth.Bindings.Any(item => item is null || item.ProjectId == Guid.Empty
                || !RepositoryId.IsValid(item.Repository) || string.IsNullOrWhiteSpace(item.NormalizedRepository))
            || auth.Bindings.Select(item => item.Repository).Distinct(StringComparer.OrdinalIgnoreCase).Count() != auth.Bindings.Length
            || auth.Bindings.Select(item => item.ProjectId).Distinct().Count() != auth.Bindings.Length
            || auth.Bindings.Select(item => item.NormalizedRepository).Distinct(StringComparer.Ordinal).Count() != auth.Bindings.Length)
            throw Error("authentication_unavailable");
    }

    private static ProviderAuthenticationException Error(string code) => new(code);
}
