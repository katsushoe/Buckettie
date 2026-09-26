using System.Security.Cryptography.X509Certificates;
using Buckettie.Application.Configuration;

namespace Buckettie.Server;

/// <summary>管理Endpoint専用の証明書境界です。Repository Assertionを代替しません。</summary>
public static class AdministratorCertificates
{
    /// <summary>明示的に信頼した有効期間内の証明書だけを受け付けます。</summary>
    public static bool IsAllowed(X509Certificate2? certificate, IEnumerable<string> thumbprints) =>
        certificate is not null
        && DateTime.UtcNow >= certificate.NotBefore.ToUniversalTime()
        && DateTime.UtcNow < certificate.NotAfter.ToUniversalTime()
        && thumbprints.Contains(certificate.Thumbprint, StringComparer.OrdinalIgnoreCase);

    /// <summary>OS Storeから秘密鍵付きの証明書を選びます。</summary>
    public static X509Certificate2 Load(string thumbprint, StoreLocation location)
    {
        using X509Store store = new(StoreName.My, location);
        store.Open(OpenFlags.ReadOnly);
        X509Certificate2[] matches = store.Certificates.Cast<X509Certificate2>()
            .Where(certificate => certificate.HasPrivateKey && IsAllowed(certificate, [thumbprint])).ToArray();
        if (matches.Length != 1) throw new InvalidOperationException("administrator_certificate_unavailable");
        return matches[0];
    }

    /// <summary>曖昧な認証設定を起動時に拒否します。</summary>
    public static void Validate(AdministratorEndpointOptions options, int mcpPort)
    {
        if (options.Port is < 1 or > 65535 || options.Port == mcpPort
            || !IsThumbprint(options.ServerCertificateThumbprint)
            || options.ClientCertificateThumbprints is null || options.ClientCertificateThumbprints.Length == 0
            || options.ClientCertificateThumbprints.Any(value => !IsThumbprint(value))
            || (options.CliCertificateThumbprint is not null && !IsThumbprint(options.CliCertificateThumbprint)))
            throw new InvalidOperationException("administrator_configuration_invalid");
    }

    private static bool IsThumbprint(string? value) => value is { Length: 40 } && value.All(char.IsAsciiHexDigit);
}
