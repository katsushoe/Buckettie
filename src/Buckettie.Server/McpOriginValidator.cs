namespace Buckettie.Server;

/// <summary>localhost MCP EndpointのOrigin境界を検証します。</summary>
internal static class McpOriginValidator
{
    /// <summary>Originが未指定または同一localhost Endpointかを検証します。</summary>
    internal static bool IsAllowed(string? origin, int port)
    {
        if (string.IsNullOrEmpty(origin))
        {
            return true;
        }

        return Uri.TryCreate(origin, UriKind.Absolute, out Uri? uri)
            && string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            && uri.IsLoopback
            && uri.Port == port
            && string.IsNullOrEmpty(uri.Query)
            && string.IsNullOrEmpty(uri.Fragment);
    }
}
